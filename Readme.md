Requires the wix tool be installed

```powershell
dotnet tool install --global wix --version 4.0.6
setx PATH "%PATH%;C:\Users\<USERNAME>\.dotnet\tools"
wix.exe extension add -g WixToolset.UI.wixext
```

Use the [download_release](https://github.com/TownSuite-DevGroup/TownSuite.MsiCreator/blob/main/download_release.ps1) script as part of build pipelines to download specific releases of msicreator.

Examples

List available options
```powershell
.\TownSuite.MsiCreator.exe -Help
```

Basic msi with a main executable
```powershell
.\TownSuite.MsiCreator.exe -CompanyName "<CompanyName>" -Product "<Product>" -Version "<Version>" -SrcBinDirectory "<SrcBinDirectory>" -OutputDirectory "<OutputDirectory>" -MainExecutable "<MainExecutable>" -ProductGuid "<ProductGuid>" -Platform "x64"
```

with product url
```powershell
.\TownSuite.MsiCreator.exe -CompanyName "<CompanyName>" -Product "<Product>" -Version "<Version>" -SrcBinDirectory "<SrcBinDirectory>" -OutputDirectory "<OutputDirectory>" -MainExecutable "<MainExecutable>" -ProductGuid "<ProductGuid>" -Platform "x64" -UrlInfoAbout "<https://example.com>" -UrlUpdateInfo "<https://example.com>"
```

Without placeholders example
```powershell
.\TownSuite.MsiCreator.exe -CompanyName "Example" -Product "Example" -Version "1.0.0" -SrcBinDirectory "C:\\Source\Directory\With\Your\Exe" -OutputDirectory "C:\\OutputDirectory" -MainExecutable "Example.exe" -ProductGuid "ee85df4f-6f19-4d3a-85bd-91b94657650f" -Platform "x64" -UrlInfoAbout "https://example.com" -UrlUpdateInfo "https://example.com"
```

A zip file, -SrcZip, can be used as a src instead of a -SrcBinDirectory.


# MSI Info

The msi by default will install per user.

## Install Per User

Users can double click the msi or run it via powershell.

```powershell
msiexec /i "MyProduct.msi"
```

Install as a non admin the filepath will be

- C:\Users\[User]\AppData\Local\Programs\[CompanyName]\[Product]


## Install Per Machine

- C:\Program Files\[CompanyName]\[Product]


To install the application machine-wide:
```powershell
msiexec /i "MyProduct.msi" ALLUSERS=1
```

