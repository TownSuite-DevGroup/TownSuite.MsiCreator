using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TownSuite.MsiCreator
{
    /// <summary>
    /// The part of the NSIS script that closes a running copy of the product before its files are
    /// replaced (install over an older version) or removed (uninstall). Without it NSIS silently
    /// skips every locked file: an uninstall "completes" with the app still running and its loaded
    /// DLLs left behind, and the next install stops on the first of them with
    /// "Error opening file for writing".
    ///
    /// Only tasklist/taskkill and the nsExec plugin that ships with NSIS are used, so no third-party
    /// plugin has to be installed on the build machine. Interactive runs ask before closing anything;
    /// a silent run (/S, the in-app updater) closes without asking. A close request (WM_CLOSE) goes
    /// first so the app can exit cleanly, and only a process that ignores it is killed.
    ///
    /// No WiX dependency, so the test project compiles this file in directly.
    /// </summary>
    internal static class NsisRunningProcesses
    {
        /// <summary>Seconds to wait for a process to honour the close request before it is killed.</summary>
        public const int GracefulCloseSeconds = 15;

        /// <summary>
        /// The image names the installer and uninstaller close: the main executable first, then any
        /// extra names, as bare file names without duplicates. Quotes can't be expressed in the
        /// generated command lines and are dropped.
        /// </summary>
        public static IReadOnlyList<string> Names(string mainExecutable, IEnumerable<string> extra)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in new[] { mainExecutable }.Concat(extra ?? Enumerable.Empty<string>()))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var name = Path.GetFileName(raw.Trim().Replace("\"", string.Empty).Replace("'", string.Empty));
                if (name.Length == 0) continue;
                if (seen.Add(name)) result.Add(name);
            }

            return result;
        }

        /// <summary>Splits a -CloseProcesses value such as "tsintel.exe;helper.exe" (';' or ',' separated).</summary>
        public static IReadOnlyList<string> ParseList(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new string[0];
            return value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>The lines that close each name, for the install section or (<paramref name="uninstaller"/>) the uninstall section.</summary>
        public static string CloseCalls(IEnumerable<string> names, bool uninstaller)
        {
            var prefix = uninstaller ? "un." : string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("  ; Close the running application so its files can be " + (uninstaller ? "removed" : "replaced"));
            sb.AppendLine($"  Call {prefix}InitCloseFilter");
            foreach (var name in names)
            {
                sb.AppendLine($"  Push \"{name}\"");
                sb.AppendLine($"  Call {prefix}CloseRunningApp");
            }
            return sb.ToString();
        }

        /// <summary>
        /// The installer and uninstaller functions; needs LogicLib.nsh included.
        /// <paramref name="currentUserOnly"/> (per-user installs) limits the search and the close to the
        /// current user's processes: on a shared desktop another user's copy, running from their own
        /// profile, holds none of this user's files and could not be closed by them anyway. A
        /// per-machine install closes every user's copy, since they all run the same files.
        /// </summary>
        public static string Functions(bool currentUserOnly)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; --- Running application handling ---");
            sb.AppendLine("; Closes a running copy of the executable whose image name is on the stack, so its files can be");
            sb.AppendLine("; replaced or removed. Asks first, except in silent mode (/S) where it closes without asking.");
            sb.AppendLine("; The process gets a close request first so it can exit cleanly; one that ignores it for");
            sb.AppendLine($"; {GracefulCloseSeconds} seconds (or has no window to close, like a console app) is killed.");
            sb.AppendLine(currentUserOnly
                ? "; Only the current user's processes count: another user's copy on a shared desktop runs its own files."
                : "; Every user's processes count: a per-machine install is shared by all of them.");
            sb.AppendLine("Var CLOSE_USER_FILTER");
            sb.AppendLine();
            AppendFunctions(sb, string.Empty, currentUserOnly);
            AppendFunctions(sb, "un.", currentUserOnly);
            return sb.ToString();
        }

        private static void AppendFunctions(StringBuilder sb, string prefix, bool currentUserOnly)
        {
            var polls = GracefulCloseSeconds * 2; // the wait loop sleeps 500 ms per poll

            // Sets $CLOSE_USER_FILTER, the extra tasklist/taskkill filter: the current user (as tasklist
            // prints them, DOMAIN\user) or nothing. Called once before the first close.
            sb.AppendLine($"Function {prefix}InitCloseFilter");
            if (currentUserOnly)
            {
                sb.AppendLine("  Push $0");
                sb.AppendLine("  Push $1");
                sb.AppendLine("  ReadEnvStr $0 USERDOMAIN");
                sb.AppendLine("  ReadEnvStr $1 USERNAME");
                sb.AppendLine("  StrCpy $CLOSE_USER_FILTER '/FI \"USERNAME eq $0\\$1\"'");
                sb.AppendLine("  Pop $1");
                sb.AppendLine("  Pop $0");
            }
            else
            {
                sb.AppendLine("  StrCpy $CLOSE_USER_FILTER \"\"");
            }
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();

            // in: $0 = image name   out: $1 = 1 when a process with that image name is running, else 0.
            // tasklist /NH /FO CSV prints a quoted CSV row per match and an unquoted INFO line (in the
            // OS language) when there is none, so the first character decides.
            sb.AppendLine($"Function {prefix}ProcessIsRunning");
            sb.AppendLine("  Push $2");
            sb.AppendLine("  nsExec::ExecToStack '\"$SYSDIR\\tasklist.exe\" /FI \"IMAGENAME eq $0\" $CLOSE_USER_FILTER /NH /FO CSV'");
            sb.AppendLine("  Pop $2 ; exit code");
            sb.AppendLine("  Pop $1 ; output");
            sb.AppendLine("  StrCpy $1 $1 1");
            sb.AppendLine("  ${If} $1 == '\"'");
            sb.AppendLine("    StrCpy $1 1");
            sb.AppendLine("  ${Else}");
            sb.AppendLine("    StrCpy $1 0");
            sb.AppendLine("  ${EndIf}");
            sb.AppendLine("  Pop $2");
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();

            // in: image name on the stack. Aborts the install/uninstall when the user cancels (exit code 1)
            // or the process survives even a kill (exit code 3).
            sb.AppendLine($"Function {prefix}CloseRunningApp");
            sb.AppendLine("  Exch $0 ; image name");
            sb.AppendLine("  Push $1");
            sb.AppendLine("  Push $2");
            sb.AppendLine("  Push $3");
            sb.AppendLine($"  Call {prefix}ProcessIsRunning");
            sb.AppendLine("  ${If} $1 == 1");
            sb.AppendLine("    MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \"$(^Name) is still running ($0) and must be closed to continue.$\\r$\\n$\\r$\\nClick OK to close it now, or Cancel to stop.\" /SD IDOK IDOK +3");
            sb.AppendLine("      SetErrorLevel 1");
            sb.AppendLine("      Abort \"$(^Name) is still running. Close it and try again.\"");
            sb.AppendLine("    DetailPrint \"Closing $0\"");
            sb.AppendLine("    nsExec::ExecToStack '\"$SYSDIR\\taskkill.exe\" $CLOSE_USER_FILTER /IM \"$0\"'");
            sb.AppendLine("    Pop $2 ; exit code: 0 when the close request was delivered");
            sb.AppendLine("    Pop $3");
            sb.AppendLine("    ${If} $2 == 0");
            sb.AppendLine("      StrCpy $2 0");
            sb.AppendLine("      ${Do}");
            sb.AppendLine("        Sleep 500");
            sb.AppendLine("        IntOp $2 $2 + 1");
            sb.AppendLine($"        Call {prefix}ProcessIsRunning");
            sb.AppendLine("        ${If} $1 == 0");
            sb.AppendLine("          ${ExitDo}");
            sb.AppendLine("        ${EndIf}");
            sb.AppendLine($"      ${{LoopUntil}} $2 >= {polls}");
            sb.AppendLine("    ${EndIf}");
            sb.AppendLine("    ${If} $1 == 1");
            sb.AppendLine("      DetailPrint \"Forcing $0 to close\"");
            // No /T: tree-kill combined with a USERNAME filter makes taskkill walk every process on the
            // machine and takes over a minute, with or without a match. Helper processes (WebView2,
            // a driven browser) exit on their own when their host goes.
            sb.AppendLine("      nsExec::ExecToStack '\"$SYSDIR\\taskkill.exe\" $CLOSE_USER_FILTER /F /IM \"$0\"'");
            sb.AppendLine("      Pop $2");
            sb.AppendLine("      Pop $3");
            sb.AppendLine("      Sleep 1000");
            sb.AppendLine($"      Call {prefix}ProcessIsRunning");
            sb.AppendLine("    ${EndIf}");
            sb.AppendLine("    ${If} $1 == 1");
            sb.AppendLine("      MessageBox MB_OK|MB_ICONSTOP \"$(^Name) could not be closed ($0). End it in Task Manager and try again.\" /SD IDOK");
            sb.AppendLine("      SetErrorLevel 3");
            sb.AppendLine("      Abort \"$(^Name) could not be closed ($0).\"");
            sb.AppendLine("    ${EndIf}");
            sb.AppendLine("  ${EndIf}");
            sb.AppendLine("  Pop $3");
            sb.AppendLine("  Pop $2");
            sb.AppendLine("  Pop $1");
            sb.AppendLine("  Pop $0");
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();
        }
    }
}
