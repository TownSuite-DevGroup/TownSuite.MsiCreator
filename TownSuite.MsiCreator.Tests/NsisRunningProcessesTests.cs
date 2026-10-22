using System;
using NUnit.Framework;
using TownSuite.MsiCreator;

namespace TownSuite.MsiCreator.Tests
{
    [TestFixture]
    public class NsisRunningProcessesTests
    {
        private const string ScriptPath = @"C:\build\out\CloseRunningApp.ps1";

        [Test]
        public void Names_MainExecutableFirst_ThenExtras_WithoutDuplicates()
        {
            var names = NsisRunningProcesses.Names(@"x64\TownSuite.exe", new[] { "tsintel.exe", "TOWNSUITE.EXE", " ", "tsintel.exe" });

            Assert.That(names, Is.EqualTo(new[] { "TownSuite.exe", "tsintel.exe" }));
        }

        [Test]
        public void Names_DropsQuotes_SoTheyCannotBreakTheCommandLine()
        {
            var names = NsisRunningProcesses.Names("\"App.exe\"", new[] { "'cli.exe'" });

            Assert.That(names, Is.EqualTo(new[] { "App.exe", "cli.exe" }));
        }

        [Test]
        public void Names_NothingToClose_IsEmpty()
        {
            Assert.That(NsisRunningProcesses.Names("", null), Is.Empty);
        }

        [Test]
        public void ParseList_SplitsOnSemicolonsAndCommas()
        {
            Assert.That(NsisRunningProcesses.ParseList(" a.exe; b.exe ,c.exe;; "), Is.EqualTo(new[] { "a.exe", "b.exe", "c.exe" }));
            Assert.That(NsisRunningProcesses.ParseList(null), Is.Empty);
        }

        [Test]
        public void CloseCalls_InstallSection_UnpacksTheHelperThenCallsTheInstallerFunction()
        {
            var calls = NsisRunningProcesses.CloseCalls(new[] { "App.exe", "cli.exe" }, uninstaller: false);

            Assert.That(calls.IndexOf("Call InitClose", StringComparison.Ordinal),
                Is.LessThan(calls.IndexOf("Call CloseRunningApp", StringComparison.Ordinal)));
            Assert.That(calls, Does.Contain("Push \"App.exe\""));
            Assert.That(calls, Does.Contain("Push \"cli.exe\""));
            Assert.That(calls, Does.Not.Contain("un."));
        }

        [Test]
        public void CloseCalls_UninstallSection_CallsTheUninstallerFunctions()
        {
            var calls = NsisRunningProcesses.CloseCalls(new[] { "App.exe" }, uninstaller: true);

            Assert.That(calls, Does.Contain("Call un.InitClose"));
            Assert.That(calls, Does.Contain("Call un.CloseRunningApp"));
            Assert.That(calls, Does.Not.Contain("Call CloseRunningApp"));
        }

        [Test]
        public void Functions_DefineInstallerAndUninstallerVariants_AndEmbedTheHelper()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: false);

            Assert.That(functions, Does.Contain("Function InitClose"));
            Assert.That(functions, Does.Contain("Function un.InitClose"));
            Assert.That(functions, Does.Contain("Function CloseRunningApp"));
            Assert.That(functions, Does.Contain("Function un.CloseRunningApp"));
            Assert.That(functions, Does.Contain($"File \"/oname=$PLUGINSDIR\\{NsisRunningProcesses.ScriptFileName}\" \"{ScriptPath}\""));
        }

        [Test]
        public void Functions_AskBeforeClosing_ButNotInSilentMode()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: false);

            Assert.That(functions, Does.Contain("MessageBox MB_OKCANCEL"));
            Assert.That(functions, Does.Contain("/SD IDOK"));
        }

        [Test]
        public void Functions_QueryBeforeClose_MatchingByInstallFolder()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: false);

            var query = functions.IndexOf("-Mode query -Dir \"$INSTDIR\" -Name \"$0\"", StringComparison.Ordinal);
            var close = functions.IndexOf($"-Mode close -Dir \"$INSTDIR\" -Name \"$0\" -Others 0 -WaitSeconds {NsisRunningProcesses.GracefulCloseSeconds}", StringComparison.Ordinal);
            Assert.That(query, Is.GreaterThan(-1));
            Assert.That(close, Is.GreaterThan(query));
            // Name-based matching is what hit side-by-side versions and unrelated copies.
            Assert.That(functions, Does.Not.Contain("IMAGENAME eq"));
            Assert.That(functions, Does.Not.Contain("/IM "));
        }

        [Test]
        public void Functions_OtherSessions_StopTheInstallByDefault()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: false);

            Assert.That(functions, Does.Contain("is open in another user's session"));
            Assert.That(functions, Does.Contain("-Others 0"));
        }

        [Test]
        public void Functions_CloseOtherSessions_IsAnOptIn()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: true);

            Assert.That(functions, Does.Not.Contain("is open in another user's session"));
            Assert.That(functions, Does.Contain("-Others 1"));
        }

        [Test]
        public void Functions_ProceedWhenTheHelperCannotRun()
        {
            var functions = NsisRunningProcesses.Functions(ScriptPath, closeOtherSessions: false);

            Assert.That(functions, Does.Contain("$1 == \"error\""));
            Assert.That(functions, Does.Contain("Could not check whether $0 is running; continuing."));
        }

        [Test]
        public void Script_MatchesByPathUnderTheInstallFolder_AndClosesBeforeKilling()
        {
            var script = NsisRunningProcesses.Script;

            Assert.That(script, Does.Contain("param([string]$Mode, [string]$Dir, [string]$Name, [int]$Others = 0, [int]$WaitSeconds = 15)"));
            Assert.That(script, Does.Contain("ExecutablePath.ToLowerInvariant().StartsWith($prefix)"));
            Assert.That(script, Does.Contain("SessionId -eq $session"));
            var gentle = script.IndexOf("taskkill.exe /PID $id", StringComparison.Ordinal);
            var forced = script.IndexOf("Stop-Process -Id $id -Force", StringComparison.Ordinal);
            Assert.That(gentle, Is.GreaterThan(-1));
            Assert.That(forced, Is.GreaterThan(gentle));
            // A user filter plus tree-kill made taskkill take over a minute; neither is used now.
            Assert.That(script, Does.Not.Contain("/T"));
            Assert.That(script, Does.Not.Contain("USERNAME eq"));
        }

        [Test]
        public void Script_AvoidsWhatConstrainedLanguageModeBlocks()
        {
            var script = NsisRunningProcesses.Script;

            // Static .NET calls and Add-Type are refused under Constrained Language Mode.
            Assert.That(script, Does.Not.Contain("]::"));
            Assert.That(script, Does.Not.Contain("Add-Type"));
            Assert.That(script, Does.Not.Contain("Invoke-Expression"));
        }

        [Test]
        public void LeftoverReport_WarnsAndSetsExitCode4_InsteadOfQueuingARebootDelete()
        {
            var function = NsisRunningProcesses.LeftoverReportFunction();

            Assert.That(function, Does.Contain("Function un.ReportLeftovers"));
            Assert.That(function, Does.Contain("FindFirst $0 $1 \"$INSTDIR\\*.*\""));
            Assert.That(function, Does.Contain("SetErrorLevel 4"));
            Assert.That(function, Does.Not.Contain("REBOOTOK"));
        }
    }
}
