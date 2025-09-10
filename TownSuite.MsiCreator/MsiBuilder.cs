
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WixSharp;

namespace TownSuite.MsiCreator
{
    internal class MsiBuilder
    {
        private readonly Config _config;

        public MsiBuilder(Config config)
        {
            _config = config;
        }

        public void Build()
        {
            var rootDir = BuildDir(_config.SrcBinDirectory, true);

            WixSharp.File mainExecutable;
            if (_config.IsService)
            {
                mainExecutable = new WixSharp.File(Path.Combine(_config.SrcBinDirectory, _config.MainExecutable));

                mainExecutable.ServiceInstaller = new ServiceInstaller
                {
                    Name = _config.ProductName,
                    StartOn = null,
                    StopOn = SvcEvent.InstallUninstall_Wait,
                    RemoveOn = SvcEvent.Uninstall_Wait,
                    Start = SvcStartType.auto,
                    DelayedAutoStart = true,
                    FirstFailureActionType = FailureActionType.restart,
                    SecondFailureActionType = FailureActionType.restart,
                    ThirdFailureActionType = FailureActionType.restart,
                    RestartServiceDelayInSeconds = 60 * 10, // 10 minutes 
                    Description =  $"{_config.CompanyName} - {_config.ProductName}"
                };
            }
            else
            {
                mainExecutable = new WixSharp.File(Path.Combine(_config.SrcBinDirectory, _config.MainExecutable))
                {
                    Shortcuts = new[]
                        {
                            new FileShortcut(_config.ProductName, "%DesktopFolder%")
                            {
                                WorkingDirectory = "INSTALLDIR",
                                Description = $"{_config.CompanyName} - {_config.ProductName}",
                            },
                            new FileShortcut(_config.ProductName, "%ProgramMenuFolder%")
                            {
                                WorkingDirectory = "INSTALLDIR",
                                Description = $"{_config.CompanyName} - {_config.ProductName}",
                            }
                        }
                };
            }

            var project = new Project(_config.ProductName,
                new Dir($@"%ProgramFiles%\{_config.CompanyName}\{_config.ProductName}",
                    rootDir,
                    mainExecutable
                )
            )
            {
                Id = Guid.NewGuid().ToString(),
                OutDir = _config.OutputDirectory,
                GUID = Guid.Parse(_config.ProductGuid),
                Version = Version.Parse(_config.ProductVersion),
                UI = WUI.WixUI_InstallDir,
                Platform = _config.Platform,

                // Set InstallScope based on ALLUSERS property
#if NET8_0_OR_GREATER
                Scope = _config.Scope
#elif NET48
                InstallScope = _config.Scope
#endif
            };

            project.ControlPanelInfo.Manufacturer = _config.CompanyName;
            project.MajorUpgradeStrategy = WixSharp.MajorUpgradeStrategy.Default;

            if (!string.IsNullOrWhiteSpace(_config.UrlInfoAbout))
            {
                project.ControlPanelInfo.UrlInfoAbout = _config.UrlInfoAbout;
            }
            if (!string.IsNullOrWhiteSpace(_config.UrlUpdateInfo))
            {
                project.ControlPanelInfo.UrlUpdateInfo = _config.UrlUpdateInfo;
            }

            project.LicenceFile = _config.LicenseFile;
            project.OutFileName =
                $"{_config.ProductName}_{_config.ProductVersion}";

#if NET48
            string wixbin = GetWixBinDir();
            Environment.SetEnvironmentVariable("WIXSHARP_WIXDIR", wixbin, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("WIXSHARP_DIR", Environment.CurrentDirectory, EnvironmentVariableTarget.Process);
#endif

            if (_config.OutputType.Equals("msi", StringComparison.OrdinalIgnoreCase))
            {
                string msiFilepath = Compiler.BuildMsi(project);
                Console.WriteLine(msiFilepath);
            }
            else if (_config.OutputType.Equals("wxs", StringComparison.OrdinalIgnoreCase))
            {
                string wxsFilepath = Compiler.BuildWxs(project);
                Console.WriteLine(wxsFilepath);
            }
        }

        Dir BuildDir(string currentDir, bool isRoot = false)
        {
            var subDirs = Directory.GetDirectories(currentDir);

#if NET48
            string fullDirPath = Path.GetFullPath(currentDir);
#else
            string fullDirPath = @"\\?\" + Path.GetFullPath(currentDir);
#endif

            var entities = new List<WixEntity>();

            if (isRoot)
            {
                // Add files directly to INSTALLDIR
                entities.Add(new DirFiles(Path.Combine(fullDirPath, "*.*")));
                // Add subdirectories as subfolders
                entities.AddRange(subDirs.Select(d => BuildDir(d)));
                return new Dir(".", entities.ToArray());
            }
            else
            {
                // Add files and subdirectories normally
                entities.Add(new DirFiles(Path.Combine(fullDirPath, "*.*")));
                entities.AddRange(subDirs.Select(d => BuildDir(d)));
                return new Dir(Path.GetFileName(currentDir), entities.ToArray());
            }
        }

#if NET48
        private string GetWixBinDir()
        {
            string wixBinPath = System.Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if ((wixBinPath == null) || (wixBinPath == ""))
            {
                wixBinPath = System.Environment.GetEnvironmentVariable("ProgramFiles");
            }
            if ((wixBinPath == null) || (wixBinPath == ""))
            {
                wixBinPath = @"C:\Program Files";
            }

            if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.14", "bin")))
            {
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.14", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.11", "bin")))
            {
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.11", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.10", "bin")))
            {
                // Attempt to use version 3.10 new directory
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.10", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.9", "bin")))
            {
                // Attempt to use version 3.9 new directory
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.9", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.8", "bin")))
            {
                // Attempt to use version 3.8 new directory
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.8", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.7", "bin")))
            {
                // Attempt to use version 3.7 new directory
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.7", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.6", "bin")))
            {
                // Attempt to use version 3.6 new directory
                wixBinPath = System.IO.Path.Combine(wixBinPath, "WiX Toolset v3.6", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "Windows Installer XML v3.6", "bin")))
            {
                // Attempt to use version 3.6
                wixBinPath = System.IO.Path.Combine(wixBinPath, "Windows Installer XML v3.6", "bin");
            }
            else if (System.IO.Directory.Exists(System.IO.Path.Combine(wixBinPath, "Windows Installer XML v3.5", "bin")))
            {
                // Attempt to use version 3.5
                wixBinPath = System.IO.Path.Combine(wixBinPath, "Windows Installer XML v3.5", "bin");
            }
            else
            {
                // Fall back to version 3
                wixBinPath = Path.Combine(wixBinPath, "Windows Installer XML v3", "bin");
            }

            return wixBinPath;
        }
#endif
    }
}
