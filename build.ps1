#!/usr/bin/env powershell
$ErrorActionPreference = "Stop"
$CURRENTPATH=$pwd.Path

# create the build output directory
if (-not (Test-Path -Path "$CURRENTPATH/build")) {
    New-Item -ItemType Directory -Path "$CURRENTPATH/build" | Out-Null
}

dotnet build -c "Release" -f "net8.0-windows"
dotnet build -c "Release" -f "net48"
#dotnet pack --configuration Release --no-build

#Get-ChildItem -Path $CURRENTPATH -Recurse -Filter "*.nupkg" | Where-Object { $_.FullName -notlike "*\build\*" } | Copy-Item -Destination "$CURRENTPATH/build" -Force