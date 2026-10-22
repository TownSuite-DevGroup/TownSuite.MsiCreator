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
    /// Processes are matched by the path they run from, not by name, so a side-by-side install of
    /// another version, a dev build or a portable copy with the same exe name is never touched. The
    /// matching and closing is done by a small PowerShell script (<see cref="Script"/>) that the
    /// installer carries along; it uses cmdlets only, so it also runs under Constrained Language
    /// Mode, and needs nothing beyond the nsExec plugin that ships with NSIS.
    ///
    /// Only the current session's copies are closed. Copies in other users' sessions cannot be
    /// asked to close (window messages do not cross sessions), so by default they stop the install
    /// with exit code 3 instead of being killed with unsaved work; <c>-CloseOtherSessions</c> opts
    /// into killing them for unattended pushes. Interactive runs ask before closing anything; a
    /// silent run (/S, the in-app updater) closes without asking. A close request (WM_CLOSE) goes
    /// first so the app can exit cleanly, and only a process that ignores it is killed.
    ///
    /// No WiX dependency, so the test project compiles this file in directly.
    /// </summary>
    internal static class NsisRunningProcesses
    {
        /// <summary>Seconds to wait for a process to honour the close request before it is killed.</summary>
        public const int GracefulCloseSeconds = 15;

        /// <summary>The helper script's file name, written next to the .nsi and carried in $PLUGINSDIR.</summary>
        public const string ScriptFileName = "CloseRunningApp.ps1";

        /// <summary>
        /// The helper. Mode query exits 0 (none), 1 (running in this session), 2 (in other sessions
        /// only) or 3 (both). Mode close exits 0 when every targeted copy is gone and 3 when one
        /// survived even a kill. Win32_Process is used rather than Get-Process because the 32-bit
        /// PowerShell the installer starts cannot read a 64-bit process's path any other way, and
        /// because it reports the session id. Another user's process shows no path to a non-admin,
        /// which is right: a per-user install never shares files with another user.
        /// </summary>
        public static readonly string Script = string.Join("\r\n", new[]
        {
            "# Finds the running copies of an executable under the install folder and closes them.",
            "# Written by TownSuite.MsiCreator; the installer carries it in $PLUGINSDIR.",
            "# -Mode query : exit 0 none, 1 running in this session, 2 in other sessions only, 3 both.",
            "# -Mode close : close this session's copies (every session's with -Others 1): a close request",
            "#               first, then a kill for whatever is still there after -WaitSeconds.",
            "#               Exit 0 when they are gone, 3 when one survived.",
            "# Cmdlets and core string methods only, so it also runs under Constrained Language Mode.",
            "param([string]$Mode, [string]$Dir, [string]$Name, [int]$Others = 0, [int]$WaitSeconds = 15)",
            "$ErrorActionPreference = 'SilentlyContinue'",
            "$prefix = ($Dir.TrimEnd('\\') + '\\').ToLowerInvariant()",
            "$session = (Get-CimInstance Win32_Process -Filter \"ProcessId = $PID\").SessionId",
            "function Find {",
            "  @(Get-CimInstance Win32_Process -Filter \"Name = '$Name'\" |",
            "    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.ToLowerInvariant().StartsWith($prefix) })",
            "}",
            "$found = Find",
            "if ($Mode -eq 'query') {",
            "  $mine = @($found | Where-Object { $_.SessionId -eq $session }).Count",
            "  $code = 0",
            "  if ($mine -gt 0) { $code += 1 }",
            "  if ($found.Count -gt $mine) { $code += 2 }",
            "  exit $code",
            "}",
            "$targets = @($found | Where-Object { $Others -eq 1 -or $_.SessionId -eq $session })",
            "if ($targets.Count -eq 0) { exit 0 }",
            "$ids = @($targets | ForEach-Object { $_.ProcessId })",
            "# Ask each one to close. taskkill reports 0 only when it could deliver the request (a window",
            "# in this session); a console process or another session's window goes straight to the kill.",
            "$waiting = @()",
            "foreach ($id in $ids) {",
            "  taskkill.exe /PID $id 2>&1 | Out-Null",
            "  if ($LASTEXITCODE -eq 0) { $waiting += $id }",
            "}",
            "$deadline = (Get-Date).AddSeconds($WaitSeconds)",
            "while ($waiting.Count -gt 0 -and (Get-Date) -lt $deadline) {",
            "  Start-Sleep -Milliseconds 500",
            "  $alive = @(Find | ForEach-Object { $_.ProcessId })",
            "  $waiting = @($waiting | Where-Object { $alive -contains $_ })",
            "}",
            "$left = @(Find | Where-Object { $ids -contains $_.ProcessId } | ForEach-Object { $_.ProcessId })",
            "foreach ($id in $left) { Stop-Process -Id $id -Force }",
            "if ($left.Count -gt 0) {",
            "  Start-Sleep -Seconds 1",
            "  $left = @(Find | Where-Object { $ids -contains $_.ProcessId } | ForEach-Object { $_.ProcessId })",
            "}",
            "if ($left.Count -gt 0) { exit 3 }",
            "exit 0",
            ""
        });

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
            sb.AppendLine($"  Call {prefix}InitClose");
            foreach (var name in names)
            {
                sb.AppendLine($"  Push \"{name}\"");
                sb.AppendLine($"  Call {prefix}CloseRunningApp");
            }
            return sb.ToString();
        }

        /// <summary>
        /// The installer and uninstaller functions; needs LogicLib.nsh included.
        /// <paramref name="scriptPath"/> is where the builder wrote <see cref="Script"/>; the
        /// installer embeds it. <paramref name="closeOtherSessions"/> is the -CloseOtherSessions opt-in.
        /// </summary>
        public static string Functions(string scriptPath, bool closeOtherSessions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; --- Running application handling ---");
            sb.AppendLine($"; {ScriptFileName} finds the copies of an executable running from $INSTDIR (by path, so another");
            sb.AppendLine("; version installed side by side or a copy elsewhere is left alone) and closes them: a close");
            sb.AppendLine($"; request first, a kill for one that ignores it for {GracefulCloseSeconds} seconds or has no window.");
            sb.AppendLine("; Asks first, except in silent mode (/S) where it closes without asking.");
            sb.AppendLine(closeOtherSessions
                ? "; -CloseOtherSessions: copies in other users' sessions are killed too (they cannot be asked)."
                : "; Copies in other users' sessions are never touched; they stop the install with exit code 3.");
            sb.AppendLine();
            AppendFunctions(sb, string.Empty, scriptPath, closeOtherSessions);
            AppendFunctions(sb, "un.", scriptPath, closeOtherSessions);
            return sb.ToString();
        }

        private static void AppendFunctions(StringBuilder sb, string prefix, string scriptPath, bool closeOtherSessions)
        {
            var others = closeOtherSessions ? 1 : 0;
            var powershell = "\"$SYSDIR\\WindowsPowerShell\\v1.0\\powershell.exe\" -NoProfile -NonInteractive -ExecutionPolicy Bypass"
                             + $" -File \"$PLUGINSDIR\\{ScriptFileName}\"";

            // Unpacks the helper script. Called once before the first close.
            sb.AppendLine($"Function {prefix}InitClose");
            sb.AppendLine("  InitPluginsDir");
            sb.AppendLine($"  File \"/oname=$PLUGINSDIR\\{ScriptFileName}\" \"{scriptPath.Replace("\"", "$\\\"")}\"");
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();

            // in: image name on the stack. Aborts the install/uninstall when another session holds the
            // product (exit code 3, unless -CloseOtherSessions), when the user cancels (exit code 1), or
            // when a process survives even a kill (exit code 3). A machine where the helper cannot run
            // (PowerShell blocked) proceeds as the installer did before this step existed.
            sb.AppendLine($"Function {prefix}CloseRunningApp");
            sb.AppendLine("  Exch $0 ; image name");
            sb.AppendLine("  Push $1");
            sb.AppendLine("  Push $2");
            sb.AppendLine($"  nsExec::ExecToStack '{powershell} -Mode query -Dir \"$INSTDIR\" -Name \"$0\"'");
            sb.AppendLine("  Pop $1 ; exit code: 0 none, 1 this session, 2 other sessions only, 3 both");
            sb.AppendLine("  Pop $2");
            sb.AppendLine("  ${If} $1 == \"error\"");
            sb.AppendLine("  ${OrIf} $1 == \"timeout\"");
            sb.AppendLine("    DetailPrint \"Could not check whether $0 is running; continuing.\"");
            sb.AppendLine("    Goto done");
            sb.AppendLine("  ${EndIf}");
            sb.AppendLine("  ${If} $1 == 0");
            sb.AppendLine("    Goto done");
            sb.AppendLine("  ${EndIf}");
            if (!closeOtherSessions)
            {
                sb.AppendLine("  ${If} $1 >= 2");
                sb.AppendLine("    MessageBox MB_OK|MB_ICONSTOP \"$(^Name) is open in another user's session ($0). Ask them to close it, then try again.\" /SD IDOK");
                sb.AppendLine("    SetErrorLevel 3");
                sb.AppendLine("    Abort \"$(^Name) is open in another user's session ($0).\"");
                sb.AppendLine("  ${EndIf}");
            }
            sb.AppendLine("  MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION \"$(^Name) is still running ($0) and must be closed to continue.$\\r$\\n$\\r$\\nClick OK to close it now, or Cancel to stop.\" /SD IDOK IDOK +3");
            sb.AppendLine("    SetErrorLevel 1");
            sb.AppendLine("    Abort \"$(^Name) is still running. Close it and try again.\"");
            sb.AppendLine("  DetailPrint \"Closing $0\"");
            sb.AppendLine($"  nsExec::ExecToStack '{powershell} -Mode close -Dir \"$INSTDIR\" -Name \"$0\" -Others {others} -WaitSeconds {GracefulCloseSeconds}'");
            sb.AppendLine("  Pop $1 ; exit code: 0 closed, 3 still running");
            sb.AppendLine("  Pop $2");
            sb.AppendLine("  ${If} $1 != 0");
            sb.AppendLine("    MessageBox MB_OK|MB_ICONSTOP \"$(^Name) could not be closed ($0). End it in Task Manager and try again.\" /SD IDOK");
            sb.AppendLine("    SetErrorLevel 3");
            sb.AppendLine("    Abort \"$(^Name) could not be closed ($0).\"");
            sb.AppendLine("  ${EndIf}");
            sb.AppendLine("  done:");
            sb.AppendLine("  Pop $2");
            sb.AppendLine("  Pop $1");
            sb.AppendLine("  Pop $0");
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();
        }

        /// <summary>
        /// The uninstaller function that reports files left in $INSTDIR after removal. A locked file
        /// used to be queued for deletion at reboot, which also deleted a reinstall done before that
        /// reboot; now it stays, and the user and the exit code (4) say so.
        /// </summary>
        public static string LeftoverReportFunction()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Reports anything still in $INSTDIR after RMDir /r (a file some other program holds open).");
            sb.AppendLine("Function un.ReportLeftovers");
            sb.AppendLine("  Push $0");
            sb.AppendLine("  Push $1");
            sb.AppendLine("  ClearErrors");
            sb.AppendLine("  FindFirst $0 $1 \"$INSTDIR\\*.*\"");
            sb.AppendLine("  IfErrors none");
            sb.AppendLine("  loop:");
            sb.AppendLine("    StrCmp $1 \"\" closed");
            sb.AppendLine("    StrCmp $1 \".\" next");
            sb.AppendLine("    StrCmp $1 \"..\" next");
            sb.AppendLine("    Goto found");
            sb.AppendLine("  next:");
            sb.AppendLine("    FindNext $0 $1");
            sb.AppendLine("    Goto loop");
            sb.AppendLine("  found:");
            sb.AppendLine("    FindClose $0");
            sb.AppendLine("    DetailPrint \"Some files in $INSTDIR are still in use and were not removed.\"");
            sb.AppendLine("    MessageBox MB_OK|MB_ICONEXCLAMATION \"Some files in $INSTDIR are still in use and were not removed. Close the program using them, then delete the folder.\" /SD IDOK");
            sb.AppendLine("    SetErrorLevel 4");
            sb.AppendLine("    Goto none");
            sb.AppendLine("  closed:");
            sb.AppendLine("    FindClose $0");
            sb.AppendLine("  none:");
            sb.AppendLine("  Pop $1");
            sb.AppendLine("  Pop $0");
            sb.AppendLine("FunctionEnd");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
