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

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(CompanyName) ||
                string.IsNullOrWhiteSpace(LicenseFile) ||
                string.IsNullOrWhiteSpace(ProductName) ||
                string.IsNullOrWhiteSpace(ProductVersion) ||
                (string.IsNullOrWhiteSpace(SrcBinDirectory) && string.IsNullOrWhiteSpace(SrcZip)) ||
                string.IsNullOrWhiteSpace(OutputDirectory) ||
                string.IsNullOrWhiteSpace(MainExecutable) ||
                string.IsNullOrWhiteSpace(ProductGuid) ||
                (OutputType != "msi" && OutputType != "wxs"))
            {
                return false;
            }
            return true;
        }
    }
}
