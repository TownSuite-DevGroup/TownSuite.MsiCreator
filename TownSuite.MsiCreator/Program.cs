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
        config.CompanyName = args[i + 1];
    }
    else if (string.Equals(args[i], "-Product", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductName = args[i + 1];
    }
    else if (string.Equals(args[i], "-Version", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductVersion = args[i + 1];
    }
    else if (string.Equals(args[i], "-SrcZip", StringComparison.OrdinalIgnoreCase))
    {
        config.SrcZip = args[i + 1];
    }
    else if (string.Equals(args[i], "-SrcBinDirectory", StringComparison.OrdinalIgnoreCase))
    {
        config.SrcBinDirectory = args[i + 1];
    }
    else if (string.Equals(args[i], "-OutputDirectory", StringComparison.OrdinalIgnoreCase))
    {
        config.OutputDirectory = args[i + 1];
    }
    else if (string.Equals(args[i], "-MainExecutable", StringComparison.OrdinalIgnoreCase))
    {
        config.MainExecutable = args[i + 1];
    }
    else if (string.Equals(args[i], "-ProductGuid", StringComparison.OrdinalIgnoreCase))
    {
        config.ProductGuid = args[i + 1];
    }
    else if (string.Equals(args[i], "-Platform", StringComparison.OrdinalIgnoreCase))
    {
        config.Platform = args[i + 1].ToLower() switch
        {
            "x64" => Platform.x64,
            "x86" => Platform.x86,
            "arm64" => Platform.arm64,
            _ => throw new ArgumentException("Invalid platform specified. Use x64, x86, or arm64.")
        };
    }
    else if (string.Equals(args[i], "-LicenseFile", StringComparison.OrdinalIgnoreCase))
    {
        config.LicenseFile = args[i + 1];
    }
    else if (string.Equals(args[i], "-OutputType", StringComparison.OrdinalIgnoreCase))
    {
        config.OutputType = args[i + 1].ToLower();
        if (config.OutputType != "msi" &&  config.OutputType != "wxs")
        {
            Console.WriteLine("Invalid output type specified. Use 'msi' or 'wxs'.");
            Environment.Exit(1);
        }
    }
    else if (string.Equals(args[i], "-UrlInfoAbout", StringComparison.OrdinalIgnoreCase))
    {
        config.UrlInfoAbout = args[i + 1];
    }
    else if (string.Equals(args[i], "-UrlUpdateInfo", StringComparison.OrdinalIgnoreCase))
    {
        config.UrlUpdateInfo = args[i + 1];
    }
    else if (string.Equals(args[i], "-Help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[i], "--help", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Usage: MsiCreator.exe -CompanyName <CompanyName> -Product <ProductName> -Version <ProductVersion> -SrcBinDirectory <SourceBinaryDirectory> -OutputDirectory <OutputDirectory> -MainExecutable <MainExecutablePath> -ProductGuid <ProductGuid> [-Platform <x64|x86|arm64>] [-LicenseFile <LicenseFilePath>]");
        Console.WriteLine("Optional parameters:");
        Console.WriteLine("  -SrcZip <SourceZipPath> : Path to a zip file containing the source binaries. If provided, it will extract to a temporary directory.");
        Console.WriteLine("  -Platform <x64|x86|arm64> : Specify the platform for the MSI (default is x64).");
        Console.WriteLine("  -LicenseFile <LicenseFilePath> : Path to the license file (default is 'LicenseTemplate.rtf').");
        Console.WriteLine("  -OutputType <msi|wxs> : Specify the output type (default is 'msi').");
        Console.WriteLine("  -UrlInfoAbout <URL> : URL for the 'Info About' link in the Control Panel.");
        Console.WriteLine("  -UrlUpdateInfo <URL> : URL for the 'Update Info' link in the Control Panel.");
        Console.WriteLine("  -Help or --help : Show this help message.");
        Console.WriteLine("Example: MsiCreator.exe -CompanyName 'My Company' -Product 'My Product' -Version '1.0.0' -SrcBinDirectory 'C:\\Path\\To\\Binaries' -OutputDirectory 'C:\\Path\\To\\Output' -MainExecutable 'MyProduct.exe' -ProductGuid '{GUID}'");
        Environment.Exit(0);
    }
}

if (!config.IsValid())
{
    Console.WriteLine("All arguments must be provided: -CompanyName, -Product, -Version, -SrcBinDirectory, -OutputDirectory, -MainExecutable, -ProductGuid");
    Environment.Exit(1);
}

try
{
    if (!String.IsNullOrWhiteSpace(config.SrcZip) && System.IO.File.Exists(config.SrcZip))
    {
        // extract the zip to a temporary directory and set the srcBinDirectory to that directory
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        System.IO.Compression.ZipFile.ExtractToDirectory(config.SrcZip, tempDir);
        config.SrcBinDirectory = tempDir;
    }

    var msiBuilder = new MsiBuilder(config);
    msiBuilder.Build();
}
finally
{
       // Clean up temporary directory if it was created
    if (!string.IsNullOrWhiteSpace(config.SrcZip) && System.IO.Directory.Exists(config.SrcBinDirectory))
    {
        try
        {
            System.IO.Directory.Delete(config.SrcBinDirectory, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up temporary directory: {ex.Message}");
        }
    }
}