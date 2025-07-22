Requires the wix tool be installed

```powershell
dotnet tool install --global wix --version 4.0.6
setx PATH "%PATH%;C:\Users\<USERNAME>\.dotnet\tools"
```


```powershell
.\TownSuite.MsiCreator.exe -CompanyName "<CompanyName>" -Product "<Product>" -Version "<Version>" -SrcBinDirectory "<SrcBinDirectory>" -OutputDirectory "<OutputDirectory>" -MainExecutable "<MainExecutable>" -ProductGuid "<ProductGuid>" -Platform "x64"
```

A zip file, -SrcZip, can be used as a src instead of a -SrcBinDirectory.


# MSI Info

Install as a non admin the filepath will be

- C:\Users\[User]\AppData\Local\Programs\[CompanyName]\[Product]


Install as an admin computer wide and the program will be installed be

- C:\Program Files\[CompanyName]\[Product]
