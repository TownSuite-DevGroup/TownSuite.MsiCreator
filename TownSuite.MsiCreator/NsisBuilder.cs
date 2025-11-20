using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TownSuite.MsiCreator
{
    internal class NsisBuilder
    {
        private readonly Config _config;

        public NsisBuilder(Config config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Build()
        {
            // Validate source directory
            if (string.IsNullOrWhiteSpace(_config.SrcBinDirectory) || !Directory.Exists(_config.SrcBinDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {_config.SrcBinDirectory}");

            Directory.CreateDirectory(_config.OutputDirectory);

            var outFileName = $"{_config.ProductName}_{_config.ProductVersion}.exe";
            var outFilePath = Path.GetFullPath(Path.Combine(_config.OutputDirectory, outFileName));
            var scriptPath = Path.Combine(_config.OutputDirectory, $"{_config.ProductName}_{_config.ProductVersion}.nsi");

            var installDirVariable = DetermineInstallDir();

            var nsi = BuildNsisScript(scriptPath, outFilePath, installDirVariable);

            File.WriteAllText(scriptPath, nsi, Encoding.UTF8);

            string makensisExe = FindMakensis();

            if (string.IsNullOrWhiteSpace(makensisExe) || !File.Exists(makensisExe))
                throw new InvalidOperationException("makensis.exe not found. Please install NSIS and ensure makensis.exe is on the PATH or in Program Files.");

            var psi = new ProcessStartInfo
            {
                FileName = makensisExe,
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _config.OutputDirectory
            };

            using (var p = new Process())
            {
                p.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Console.WriteLine(e.Data);
                };
                p.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Console.WriteLine(e.Data);
                };
                p.StartInfo = psi;
                p.Start();
                p.BeginOutputReadLine();
                p.WaitForExit();

                if (p.ExitCode != 0)
                    throw new Exception($"makensis failed with exit code {p.ExitCode}");
            }

            Console.WriteLine(outFilePath);
        }

        private string DetermineInstallDir()
        {
            // Map InstallScope to NSIS Install dir and required privilege level.
            // Default to per-user install if explicitly perUser; otherwise use per-machine.
            var scope = _config.InstallScopeToString();

            if (string.Equals(scope, "perUser", StringComparison.OrdinalIgnoreCase))
                return "$LOCALAPPDATA\\$Company\\$Product";
            else
                return "$PROGRAMFILES\\$Company\\$Product";
        }

        private string BuildNsisScript(string scriptPath, string outExePath, string installDirVariable)
        {
            var srcRoot = Path.GetFullPath(_config.SrcBinDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Use ProductGuid if supplied, otherwise generate a GUID to use as the Uninstall key name
            var rawProductKey = !string.IsNullOrWhiteSpace(_config.ProductGuid)
                ? _config.ProductGuid.Trim()
                : Guid.NewGuid().ToString("B"); // {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}

            // Strip braces to avoid potential parsing issues in registry key name
            var productKey = rawProductKey.Trim('{', '}');

            // Choose registry root according to install scope
            var isPerUser = string.Equals(_config.InstallScopeToString(), "perUser", StringComparison.OrdinalIgnoreCase);
            var regRoot = isPerUser ? "HKCU" : "HKLM";
            var uninstallRegKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{productKey}";

            var sb = new StringBuilder();

            // Modern UI 2
            sb.AppendLine("!include \"MUI2.nsh\"");
            sb.AppendLine();

            sb.AppendLine($"Name \"{EscapeForNsisString(_config.ProductName)}\"");
            sb.AppendLine($"OutFile \"{EscapeForNsisString(outExePath)}\"");
            sb.AppendLine($"InstallDir \"{installDirVariable.Replace("$Company", _config.CompanyName).Replace("$Product", _config.ProductName)}\"");
            sb.AppendLine();

            // Request admin rights when installing to Program Files or when service installation is required
            if (!string.Equals(_config.InstallScopeToString(), "perUser", StringComparison.OrdinalIgnoreCase) || _config.IsService)
                sb.AppendLine("RequestExecutionLevel admin");
            else
                sb.AppendLine("RequestExecutionLevel user");

            sb.AppendLine();

            // Pages
            if (!string.IsNullOrWhiteSpace(_config.LicenseFile) && File.Exists(_config.LicenseFile))
            {
                var lic = Path.GetFullPath(_config.LicenseFile);
                sb.AppendLine($"!define MUI_PAGE_LICENSE \"{EscapeForNsisString(lic)}\"");
            }
            sb.AppendLine("!insertmacro MUI_PAGE_WELCOME");
            sb.AppendLine("!insertmacro MUI_PAGE_DIRECTORY");
            sb.AppendLine("!insertmacro MUI_PAGE_INSTFILES");
            sb.AppendLine("!insertmacro MUI_PAGE_FINISH");
            sb.AppendLine();
            sb.AppendLine("!insertmacro MUI_UNPAGE_CONFIRM");
            sb.AppendLine("!insertmacro MUI_UNPAGE_INSTFILES");
            sb.AppendLine();

            // Vars
            sb.AppendLine("Var STARTMENU_DIR");
            sb.AppendLine();

            // Installer section
            sb.AppendLine("Section \"Install\"");
            sb.AppendLine("  SetOutPath \"$INSTDIR\"");

            // Copy files recursively from source
            sb.AppendLine($"  File /r \"{EscapeForNsisString(Path.Combine(srcRoot, "*.*"))}\"");
            sb.AppendLine();

            // Create shortcuts (desktop + start menu) for non-service
            if (!_config.IsService)
            {
                sb.AppendLine($"  CreateShortCut \"$DESKTOP\\{EscapeForNsisString(_config.ProductName)}.lnk\" \"$INSTDIR\\{EscapeForNsisString(_config.MainExecutable)}\" \"\" \"$INSTDIR\\{EscapeForNsisString(_config.MainExecutable)}\" 0");
                sb.AppendLine();

                sb.AppendLine($"  StrCpy $STARTMENU_DIR \"$SMPROGRAMS\\{EscapeForNsisString(_config.CompanyName)}\\{EscapeForNsisString(_config.ProductName)}\"");
                sb.AppendLine("  CreateDirectory \"$STARTMENU_DIR\"");
                sb.AppendLine($"  CreateShortCut \"$STARTMENU_DIR\\{EscapeForNsisString(_config.ProductName)}.lnk\" \"$INSTDIR\\{EscapeForNsisString(_config.MainExecutable)}\" \"\" \"$INSTDIR\\{EscapeForNsisString(_config.MainExecutable)}\" 0");
                sb.AppendLine();
            }

            // Service install (using sc.exe)
            if (_config.IsService)
            {
                var serviceName = _config.ProductName;
                var svcExe = $"$INSTDIR\\{_config.MainExecutable}";
                var desc = $"{_config.CompanyName} - {_config.ProductName}";

                sb.AppendLine($"  ; Install Windows service using sc.exe");
                sb.AppendLine($"  ExecWait 'sc create \"{EscapeForNsisString(serviceName)}\" binPath= \"{EscapeForNsisString(svcExe)}\" start= auto' $0");
                sb.AppendLine($"  ExecWait 'sc description \"{EscapeForNsisString(serviceName)}\" \"{EscapeForNsisString(desc)}\"' $0");
                sb.AppendLine($"  ExecWait 'sc start \"{EscapeForNsisString(serviceName)}\"' $0");
                sb.AppendLine();
            }

            // Write uninstaller executable
            sb.AppendLine($"  ; Write the uninstaller to $INSTDIR\\Uninstall.exe");
            sb.AppendLine($"  WriteUninstaller \"$INSTDIR\\Uninstall.exe\"");
            sb.AppendLine();

            // Register the product in ARP (Programs and Features)
            sb.AppendLine($"  ; Register in Add/Remove Programs ({regRoot})");
            // Use helper to guarantee correct four-parameter formatting and avoid accidental concatenation that produces 5 tokens.
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "DisplayName", _config.ProductName);
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "UninstallString", "$INSTDIR\\Uninstall.exe");
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "DisplayVersion", _config.ProductVersion);
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "Publisher", _config.CompanyName);
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "InstallLocation", "$INSTDIR");
            if (!string.IsNullOrWhiteSpace(_config.UrlInfoAbout))
                AppendWriteRegStr(sb, regRoot, uninstallRegKey, "URLInfoAbout", _config.UrlInfoAbout);
            if (!string.IsNullOrWhiteSpace(_config.UrlUpdateInfo))
                AppendWriteRegStr(sb, regRoot, uninstallRegKey, "HelpLink", _config.UrlUpdateInfo);
            if (!string.IsNullOrWhiteSpace(_config.MainExecutable))
                AppendWriteRegStr(sb, regRoot, uninstallRegKey, "DisplayIcon", "$INSTDIR\\" + _config.MainExecutable);
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "NoModify", "1");
            AppendWriteRegStr(sb, regRoot, uninstallRegKey, "NoRepair", "1");
            sb.AppendLine();

            sb.AppendLine("SectionEnd");
            sb.AppendLine();

            // Uninstall section
            sb.AppendLine("Section \"Uninstall\"");
            if (_config.IsService)
            {
                var serviceName = _config.ProductName;
                sb.AppendLine($"  ; stop and delete service");
                sb.AppendLine($"  ExecWait 'sc stop \"{EscapeForNsisString(serviceName)}\"' $0");
                sb.AppendLine($"  ExecWait 'sc delete \"{EscapeForNsisString(serviceName)}\"' $0");
                sb.AppendLine();
            }

            if (!_config.IsService)
            {
                sb.AppendLine($"  Delete \"$DESKTOP\\{EscapeForNsisString(_config.ProductName)}.lnk\"");
                sb.AppendLine($"  Delete \"$SMPROGRAMS\\{EscapeForNsisString(_config.CompanyName)}\\{EscapeForNsisString(_config.ProductName)}\\{EscapeForNsisString(_config.ProductName)}.lnk\"");
                sb.AppendLine($"  RMDir /r \"$SMPROGRAMS\\{EscapeForNsisString(_config.CompanyName)}\\{EscapeForNsisString(_config.ProductName)}\"");
                sb.AppendLine();
            }

            sb.AppendLine("  RMDir /r \"$INSTDIR\"");
            sb.AppendLine();

            sb.AppendLine($"  ; Remove Add/Remove Programs registration");
            sb.AppendLine($"  DeleteRegKey {regRoot} \"{uninstallRegKey}\"");
            sb.AppendLine();

            sb.AppendLine("SectionEnd");
            sb.AppendLine();

            return sb.ToString();
        }

        // Helper to emit a correctly formatted WriteRegStr line (exactly 4 parameters).
        private void AppendWriteRegStr(StringBuilder sb, string rootKey, string subKey, string entryName, string value)
        {
            if (string.IsNullOrWhiteSpace(rootKey)) rootKey = "HKLM";
            rootKey = rootKey.Trim(); // ensure no accidental trailing slash
            // Validate root key starts with HK* to avoid accidental concatenation issues
            if (!rootKey.StartsWith("HK", StringComparison.OrdinalIgnoreCase))
                rootKey = "HKLM";

            // Ensure subKey has no surrounding quotes and is not accidentally glued to rootKey
            subKey = subKey.Trim();

            // Value may itself contain quotes (e.g. the UninstallString); allow them but escape internal quotes for NSIS
            var escapedValue = EscapeForNsisString(value);

            // Emit exactly 4 parameters to WriteRegStr
            sb.AppendLine($"  WriteRegStr {rootKey} \"{subKey}\" \"{EscapeForNsisString(entryName)}\" \"{escapedValue}\"");
        }

        private static string EscapeForNsisString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            // Only escape double quotes for safe embedding inside NSIS quoted strings.
            return input.Replace("\"", "\\\"");
        }

        private string FindMakensis()
        {
            // Prefer makensis on PATH
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "makensis.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    var first = outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first) && File.Exists(first))
                        return first;
                }
            }
            catch
            {
                // ignore
            }

            // Try standard install locations
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var candidates = new[]
            {
                Path.Combine(programFiles, "NSIS", "makensis.exe")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }

            return null;
        }
    }
}
