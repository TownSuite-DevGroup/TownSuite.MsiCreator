using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TownSuite.MsiCreator;
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
            var rootDir = BuildDir(_config.SrcBinDirectory);

            var project = new Project(_config.ProductName,
                new Dir(@$"%ProgramFiles%\{_config.CompanyName}\{_config.ProductName}",
                    rootDir
                )
            )
            {
                Id = Guid.NewGuid().ToString(),
                OutDir = _config.OutputDirectory,
                GUID = Guid.Parse(_config.ProductGuid),
                Version = Version.Parse(_config.ProductVersion),
                UI = WUI.WixUI_InstallDir,
                Platform = _config.Platform,
                Scope = InstallScope.perUserOrMachine
            };

            project.ControlPanelInfo.Manufacturer = _config.CompanyName;
            
            project.LicenceFile = _config.LicenseFile;
            project.OutFileName = $"{_config.ProductName}_{_config.ProductVersion}";
            
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

        Dir BuildDir(string currentDir)
        {
            var subDirs = Directory.GetDirectories(currentDir);

            // long paths (if enabled) by prefixing with \\?\
            // see https://github.com/wixtoolset/issues/issues/9115
            string fullDirPath = @"\\?\" + Path.GetFullPath(currentDir);

            var entities = new List<WixEntity>
            {
                new DirFiles(Path.Combine(fullDirPath, "*.*"))
            };

            entities.AddRange(subDirs.Select(d => BuildDir(d)));

            return new Dir(Path.GetFileName(currentDir), entities.ToArray());
        }
    }
}