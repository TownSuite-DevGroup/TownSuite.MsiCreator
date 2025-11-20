using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WixSharp;

namespace TownSuite.MsiCreator
{
    internal class Config
    {
        public string CompanyName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductVersion { get; set; } = string.Empty;
        public string SrcBinDirectory { get; set; } = string.Empty;
        public string SrcZip { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
        public string MainExecutable { get; set; } = string.Empty;
        public string ProductGuid { get; set; } = string.Empty;
        public Platform Platform { get; set; } = Platform.x64; // Default platform
        public string LicenseFile { get; set; } = System.IO.Path.Combine(AppContext.BaseDirectory, "LicenseTemplate.rtf"); // Default license file from running exe directory
        public string OutputType { get; set; } = "msi"; // Default output type
        public string UrlInfoAbout { get; set; } = string.Empty;
        public string UrlUpdateInfo { get; set; } = string.Empty;
        public bool IsService { get; set; } = false;

#if NET8_0_OR_GREATER
        /// <summary>
        /// Valid values are "perUser", "perMachine" or "perUserOrMachine" (default on .NET 8.0 or greater).
        /// </summary>
        public InstallScope Scope { get; set; } = InstallScope.perUserOrMachine;
#else
        /// <summary>
        /// Valid values are "perUser" (default net48), "perMachine" or "perUserOrMachine" (default on .NET 8.0 or greater).
        /// </summary>
        public InstallScope Scope { get; set; } = InstallScope.perUser;
#endif



        public string InstallScopeToString()
        {
            return Scope.ToString();
        }

        public (bool Valid, string Message) IsValid()
        {
            var missingFields = new List<string>();

            if (string.IsNullOrWhiteSpace(CompanyName))
                missingFields.Add("CompanyName");

            if (string.IsNullOrWhiteSpace(LicenseFile))
                missingFields.Add("LicenseFile");

            if (string.IsNullOrWhiteSpace(ProductName))
                missingFields.Add("ProductName");

            if (string.IsNullOrWhiteSpace(ProductVersion))
                missingFields.Add("ProductVersion");

            if (string.IsNullOrWhiteSpace(SrcBinDirectory) && string.IsNullOrWhiteSpace(SrcZip))
                missingFields.Add("SrcBinDirectory or SrcZip");

            if (string.IsNullOrWhiteSpace(OutputDirectory))
                missingFields.Add("OutputDirectory");

            if (string.IsNullOrWhiteSpace(MainExecutable))
                missingFields.Add("MainExecutable");

            if (string.IsNullOrWhiteSpace(ProductGuid))
                missingFields.Add("ProductGuid");

            if (OutputType != "msi" && OutputType != "wxs" && OutputType != "exe")
                missingFields.Add("OutputType (must be 'msi', 'wxs', or 'exe')");

            if (missingFields.Count > 0)
            {
                return (false, $"Missing required fields: {string.Join(", ", missingFields)}");
            }

            return (true, "");
        }
    }
}
