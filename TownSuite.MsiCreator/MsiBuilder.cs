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

            var project = new ManagedProject(_config.ProductName,
                new Dir(@$"%ProgramFiles%\{_config.CompanyName}\{_config.ProductName}",
                    rootDir,
                    new WixSharp.File(Path.Combine(_config.SrcBinDirectory, _config.MainExecutable))
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
                    }
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
                Scope = InstallScope.perUserOrMachine,
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

            string fullDirPath = @"\\?\" + Path.GetFullPath(currentDir);

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
    }
}