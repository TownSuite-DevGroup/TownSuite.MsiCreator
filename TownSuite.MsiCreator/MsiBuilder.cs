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

        public void Build(Config config)
        {
            var rootDir = BuildDir(config.SrcBinDirectory, config.SrcBinDirectory);

            var project = new Project(config.ProductName,
                                      new Dir(@$"%ProgramFiles%\{config.CompanyName}\{config.ProductName}",
                                        rootDir
                                        )
                                   )
            {
                Id = Guid.NewGuid().ToString(),
                OutDir = config.OutputDirectory,
                GUID = Guid.Parse(config.ProductGuid),
                Version = Version.Parse(config.ProductVersion),
                UI = WUI.WixUI_Minimal,
                Platform = config.Platform,
                Scope = InstallScope.perUserOrMachine
            };

            project.ControlPanelInfo.Manufacturer = config.CompanyName;

            if (!string.IsNullOrEmpty(config.LicenseFile) && System.IO.File.Exists(config.LicenseFile))
            {
                project.LicenceFile = config.LicenseFile;
            }

            string wixbin = GetWixBinDir();
            Environment.SetEnvironmentVariable("WIXSHARP_WIXDIR", wixbin, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("WIXSHARP_DIR", Environment.CurrentDirectory, EnvironmentVariableTarget.Process);

            if (config.OutputType.Equals("msi", StringComparison.OrdinalIgnoreCase))
            {
                project.OutFileName = $"{config.ProductName}_{config.ProductVersion}.msi";
                string msiFilepath = Compiler.BuildMsi(project);
                Console.WriteLine(msiFilepath);
            }
            else if (config.OutputType.Equals("wxs", StringComparison.OrdinalIgnoreCase))
            {
                project.OutFileName = $"{config.ProductName}_{config.ProductVersion}.wxs";
                string wxsFilepath = Compiler.BuildWxs(project);
                Console.WriteLine(wxsFilepath);
            }
        }

string GetWixBinDir()
{
    string rval = System.Environment.GetEnvironmentVariable("ProgramFiles(x86)");
    if ((rval == null) || (rval == ""))
    {
        rval = System.Environment.GetEnvironmentVariable("ProgramFiles");
    }
    if ((rval == null) || (rval == ""))
    {
        rval = @"C:\Program Files";
    }

    if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.11", "bin")))
    {
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.11", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.10", "bin")))
    {
        // Attempt to use version 3.10 new directory
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.10", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.9", "bin")))
    {
        // Attempt to use version 3.9 new directory
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.9", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.8", "bin")))
    {
        // Attempt to use version 3.8 new directory
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.8", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.7", "bin")))
    {
        // Attempt to use version 3.7 new directory
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.7", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "WiX Toolset v3.6", "bin")))
    {
        // Attempt to use version 3.6 new directory
        rval = System.IO.Path.Combine(rval, "WiX Toolset v3.6", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "Windows Installer XML v3.6", "bin")))
    {
        // Attempt to use version 3.6
        rval = System.IO.Path.Combine(rval, "Windows Installer XML v3.6", "bin");
    }
    else if (System.IO.Directory.Exists(System.IO.Path.Combine(rval, "Windows Installer XML v3.5", "bin")))
    {
        // Attempt to use version 3.5
        rval = System.IO.Path.Combine(rval, "Windows Installer XML v3.5", "bin");
    }
    else
    {
        // Fall back to version 3
        rval = Path.Combine(rval, "Windows Installer XML v3", "bin");
    }

    return rval;
}

Dir BuildDir(string baseDir, string currentDir)
{
    var subDirs = Directory.GetDirectories(currentDir);
    var entities = new List<WixEntity>();
    entities.AddRange(Directory.GetFiles(currentDir).Select(f => new WixSharp.File(f)));

    return new Dir(Path.GetFileName(currentDir),
        entities.Concat(subDirs.Select(d => BuildDir(baseDir, d))).ToArray()
        );
}

    }
}
