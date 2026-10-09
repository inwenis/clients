#Requires -Version 7
$ErrorActionPreference = 'Stop'
./tools/version-fsx-check.ps1
dotnet build
