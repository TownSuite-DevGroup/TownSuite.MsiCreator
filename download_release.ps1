#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

# Settings, see https://github.com/TownSuite-DevGroup/TownSuite.MsiCreator/releases
$URL="https://github.com/TownSuite-DevGroup/TownSuite.MsiCreator/releases/download/version_1.0.1/win-anycpu.zip"
$expectedHash = "251fc98ead2d8ff5ff55df21d2cfd646775764a389c300f7e0ef795234ec2007"
$filePath = "build\TownSuite.MsiCreator.zip"
$DestinationPath = "build\TownSuite.MsiCreator"

## Download, verify
Write-Output "Downloading TownSuite.MsiCreator executable..."
Invoke-WebRequest -Uri "$URL" -OutFile "$filePath"


$actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash.ToLower()
Write-Output "Verifying the hash of TownSuite.MsiCreator..."
if ($actualHash -ne $expectedHash) {
    Write-Output "Hash verification failed for TownSuite.MsiCreator. Exiting."
    exit 1
}
Write-Output "Hash verification succeeded for TownSuite.MsiCreator."


# Extract
Write-Output "Extracting TownSuite.MsiCreator..."
Expand-Archive -Path "$filePath" -DestinationPath "$DestinationPath" -Force
Write-Output "Extraction completed."
