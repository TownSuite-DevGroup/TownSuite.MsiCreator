using NUnit.Framework;
using TownSuite.MsiCreator;

namespace TownSuite.MsiCreator.Tests
{
    [TestFixture]
    public class NsisRunningProcessesTests
    {
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
        public void CloseCalls_InstallSection_CallsTheInstallerFunction()
        {
            var calls = NsisRunningProcesses.CloseCalls(new[] { "App.exe", "cli.exe" }, uninstaller: false);

            Assert.That(calls, Does.Contain("Push \"App.exe\"\n  Call CloseRunningApp").Or.Contain("Push \"App.exe\"\r\n  Call CloseRunningApp"));
            Assert.That(calls, Does.Contain("Push \"cli.exe\""));
            Assert.That(calls, Does.Not.Contain("un.CloseRunningApp"));
        }

        [Test]
        public void CloseCalls_UninstallSection_CallsTheUninstallerFunction()
        {
            var calls = NsisRunningProcesses.CloseCalls(new[] { "App.exe" }, uninstaller: true);

            Assert.That(calls, Does.Contain("Call un.CloseRunningApp"));
            Assert.That(calls, Does.Not.Contain("Call CloseRunningApp"));
        }

        [Test]
        public void Functions_DefineInstallerAndUninstallerVariants()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: true);

            Assert.That(functions, Does.Contain("Function CloseRunningApp"));
            Assert.That(functions, Does.Contain("Function un.CloseRunningApp"));
            Assert.That(functions, Does.Contain("Function ProcessIsRunning"));
            Assert.That(functions, Does.Contain("Function un.ProcessIsRunning"));
            Assert.That(functions, Does.Contain("Function InitCloseFilter"));
            Assert.That(functions, Does.Contain("Function un.InitCloseFilter"));
        }

        [Test]
        public void Functions_AskBeforeClosing_ButNotInSilentMode()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: true);

            Assert.That(functions, Does.Contain("MessageBox MB_OKCANCEL"));
            Assert.That(functions, Does.Contain("/SD IDOK"));
        }

        [Test]
        public void Functions_RequestACloseBeforeKilling()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: true);

            var gentle = functions.IndexOf("taskkill.exe\" $CLOSE_USER_FILTER /IM", System.StringComparison.Ordinal);
            var forced = functions.IndexOf("taskkill.exe\" $CLOSE_USER_FILTER /F /IM", System.StringComparison.Ordinal);
            Assert.That(gentle, Is.GreaterThan(-1));
            Assert.That(forced, Is.GreaterThan(gentle));
            // Tree kill plus a user filter makes taskkill take over a minute even with nothing to kill.
            Assert.That(functions, Does.Not.Contain("/T /IM"));
            Assert.That(functions, Does.Contain($"$2 >= {NsisRunningProcesses.GracefulCloseSeconds * 2}"));
        }

        [Test]
        public void Functions_PerUserInstall_OnlyTouchesTheCurrentUsersProcesses()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: true);

            Assert.That(functions, Does.Contain("ReadEnvStr $0 USERDOMAIN"));
            Assert.That(functions, Does.Contain("ReadEnvStr $1 USERNAME"));
            Assert.That(functions, Does.Contain("USERNAME eq $0\\$1"));
        }

        [Test]
        public void Functions_PerMachineInstall_TouchesEveryUsersProcesses()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: false);

            Assert.That(functions, Does.Not.Contain("USERNAME eq"));
            Assert.That(functions, Does.Contain("StrCpy $CLOSE_USER_FILTER \"\""));
        }

        [Test]
        public void CloseCalls_InitialiseTheUserFilterFirst()
        {
            var calls = NsisRunningProcesses.CloseCalls(new[] { "App.exe" }, uninstaller: true);

            Assert.That(calls.IndexOf("Call un.InitCloseFilter", System.StringComparison.Ordinal),
                Is.LessThan(calls.IndexOf("Call un.CloseRunningApp", System.StringComparison.Ordinal)));
        }

        [Test]
        public void Functions_UseOnlyWhatShipsWithNsis()
        {
            var functions = NsisRunningProcesses.Functions(currentUserOnly: true);

            Assert.That(functions, Does.Contain("nsExec::ExecToStack"));
            Assert.That(functions, Does.Not.Contain("nsProcess"));
            Assert.That(functions, Does.Not.Contain("FindProcDLL"));
        }
    }
}
