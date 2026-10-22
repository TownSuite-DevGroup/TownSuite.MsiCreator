Requires the wix tool be installed

```powershell
dotnet tool install --global wix --version 4.0.6
setx PATH "%PATH%;C:\Users\<USERNAME>\.dotnet\tools"
$dotnetToolsPath = "$env:USERPROFILE\.dotnet\tools"
$env:PATH += ";$dotnetToolsPath"
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

`-Src` takes the build to package as a folder, a `.zip`, or a tar archive compressed with Zstandard (`.tar.zst`), gzip (`.tar.gz`) or not at all (`.tar`). The format is read from the file's content, not its name. `-SrcZip` and `-SrcBinDirectory` still work and mean the same thing.

An archive is extracted to a temporary folder that is deleted afterwards; a folder is used in place and never deleted. When the source holds just one folder and `-MainExecutable` is inside it, as with TownSuite release packages whose files sit under `x64\`, that folder is used as the root.

Tar archives may be ustar, GNU or pax format (what GNU tar, bsdtar and .NET write). Entries that would land outside the extraction folder, links, and device files are rejected.

NSIS exe with main executable

```powershell
.\TownSuite.MsiCreator.exe -OutputType "exe" -CompanyName "<CompanyName>" -Product "<Product>" -Version "<Version>" -SrcBinDirectory "<SrcBinDirectory>" -OutputDirectory "<OutputDirectory>" -MainExecutable "<MainExecutable>" -ProductGuid "<ProductGuid>" -Platform "x64"
```

The exe installer and its uninstaller close a running copy of the product before they replace or remove any file, so an install over an older version, or an uninstall from Add/Remove Programs, works while the app is open. The user is asked first; a silent run (`/S`) closes it without asking. The app gets a close request and a few seconds to exit on its own before it is killed. `-CloseProcesses "tsintel.exe;other.exe"` names further executables that ship in the same folder and hold its files open. Exit code 1 means the user cancelled at that prompt, 3 that a process could not be closed.



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

