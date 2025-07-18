Requires the wix tool be installed

```powershell
dotnet tool install --global wix --version 4.0.6
setx PATH "%PATH%;C:\Users\<USERNAME>\.dotnet\tools"
```


```powershell
.\TownSuite.MsiCreator.exe -CompanyName "<CompanyName>" -Product "<Product>" -Version "<Version>" -SrcBinDirectory "<SrcBinDirectory>" -OutputDirectory "<OutputDirectory>" -MainExecutable "<MainExecutable>" -ProductGuid "<ProductGuid>" -Platform "x64"
```

A zip file, -SrcZip, can be used as a src instead of a -SrcBinDirectory.

