// Takes as input -CompanyName, -Product, -Version, -SrcBinDirectory, -OutputDirectory, and -MainExecutable
// and creates a WiX project to build an MSI installer for the product.
// The project structure includes directories for tools, documentation, and binaries.
// The output MSI will be placed in the specified output directory.
// The project is configured with a unique GUID for identification.
// The WiX project is built using the WixSharp library, which simplifies the creation of MSI installers in C#.
// The project includes a main executable and a manual file in the documentation directory.
using System;
using System.Linq;
using TownSuite.MsiCreator;
using WixSharp;
using WixSharp.CommonTasks;

var config = new Config();

for (int i = 0; i< args.Length; i++)
{
    if (string.Equals(args[i], "-CompanyName", StringComparison.OrdinalIgnoreCase))
    {
        config.CompanyName = args[++i];
    }
    else if (string.Equals(args[i], "-Product", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductName = args[++i];
    }
    else if (string.Equals(args[i], "-Version", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductVersion = args[++i];
    }
    else if (string.Equals(args[i], "-SrcBinDirectory", StringComparison.OrdinalIgnoreCase))
    {
        config.SrcBinDirectory = args[++i];
    }
    else if (string.Equals(args[i], "-OutputDirectory", StringComparison.OrdinalIgnoreCase))
    {
        config.OutputDirectory = args[++i];
    }
    else if (string.Equals(args[i], "-MainExecutable", StringComparison.OrdinalIgnoreCase))
    {
        config.MainExecutable = args[++i];
    }
    else if (string.Equals(args[i], "-ProductGuid", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductGuid = args[++i];
    }
    else if (string.Equals(args[i], "-Platform", StringComparison.OrdinalIgnoreCase))
    {
        config.Platform = args[++i].ToLower() switch
        {
            "x64" => Platform.x64,
            "x86" => Platform.x86,
            "arm64" => Platform.arm64,
            _ => throw new ArgumentException("Invalid platform specified. Use x64, x86, or arm64.")
        };
    }
    else if (string.Equals(args[i], "-LicenseFile", StringComparison.OrdinalIgnoreCase))
    {
        config.LicenseFile = args[++i];
    }
    else if (string.Equals(args[i], "-OutputType", StringComparison.OrdinalIgnoreCase))
    {
        config.OutputType = args[++i].ToLower();
        if (config.OutputType != "msi" &&  config.OutputType != "wxs")
        {
            Console.WriteLine("Invalid output type specified. Use 'msi' or 'wxs'.");
            Environment.Exit(1);
        }
    }
    else if (string.Equals(args[i], "-Help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[i], "--help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Usage: MsiCreator.exe -CompanyName <CompanyName> -Product <ProductName> -Version <ProductVersion> -SrcBinDirectory <SourceBinaryDirectory> -OutputDirectory <OutputDirectory> -MainExecutable <MainExecutablePath> -ProductGuid <ProductGuid> [-Platform <x64|x86|arm64>] [-LicenseFile <LicenseFilePath>]");
        Environment.Exit(0);
    }
    else
    {
        Console.Write($"Unknown argument: {args[i]}");
        Environment.Exit(1);
    }
}

if (!config.IsValid())
{
    Console.WriteLine("All arguments must be provided: -CompanyName, -Product, -Version, -SrcBinDirectory, -OutputDirectory, -MainExecutable, -ProductGuid");
    Environment.Exit(1);
}


// Usage:
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