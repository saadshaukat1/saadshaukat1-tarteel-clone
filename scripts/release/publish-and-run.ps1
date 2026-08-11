[CmdletBinding()]
param(
    [string]$ProjectPath = "mobile/TarteelMobile/TarteelMobile.csproj",
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net9.0-windows10.0.19041.0",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot = "artifacts/windows-packaging",
    [string]$ModelDir = "offline-assets/models",
    [string]$DataDir = "offline-assets/data",
    [string]$ExtrasDir = "offline-assets/extras",
    [switch]$SelfContained,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $repoRoot

$projectFullPath = Join-Path $repoRoot $ProjectPath
$outputRootFullPath = Join-Path $repoRoot $OutputRoot
$modelDirFullPath = Join-Path $repoRoot $ModelDir
$dataDirFullPath = Join-Path $repoRoot $DataDir
$extrasDirFullPath = Join-Path $repoRoot $ExtrasDir
$publishDir = Join-Path $outputRootFullPath "publish"

Write-Host "=== Publish and Run ==="
Write-Host "Project: $projectFullPath"
Write-Host "Framework: $TargetFramework"
Write-Host "RID: $RuntimeIdentifier"
Write-Host "Configuration: $Configuration"
Write-Host "Self-contained: $($SelfContained.IsPresent)"
Write-Host "Publish output: $publishDir"

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "=== Publishing ==="

    $publishArgs = @(
        "publish",
        $projectFullPath,
        "-c", $Configuration,
        "-f", $TargetFramework,
        "-r", $RuntimeIdentifier,
        "-p:OfflineModelDir=$modelDirFullPath",
        "-p:OfflineDataDir=$dataDirFullPath",
        "-p:OfflineExtrasDir=$extrasDirFullPath",
        "-p:AppxBundle=Never",
        "-p:WindowsPackageType=None",
        "-p:GenerateAppxPackageOnBuild=false",
        "-p:SelfContained=$($SelfContained.IsPresent.ToString().ToLowerInvariant())",
        "-o", $publishDir
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}

# Locate the executable in the publish output
$exeName = "TarteelMobile.exe"
$exePath = Join-Path $publishDir $exeName

if (-not (Test-Path -LiteralPath $exePath)) {
    # Fallback: search for any .exe in the publish dir
    $exePath = Get-ChildItem -Path $publishDir -Filter "*.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
    if (-not $exePath) {
        throw "Executable not found in publish output: $publishDir"
    }
}

Write-Host ""
Write-Host "=== Running ==="
Write-Host "Executable: $exePath"
Write-Host ""

& $exePath
