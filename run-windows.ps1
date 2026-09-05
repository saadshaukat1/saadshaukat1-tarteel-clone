# run-windows.ps1 — Build and run Tarteel MAUI on Windows
# Usage:
#   .\run-windows.ps1 [-Configuration Debug|Release] [-Clean] [-Publish]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
Set-Location $repoRoot

$projectPath = Join-Path $repoRoot "mobile\TarteelMobile\TarteelMobile.csproj"
$targetFramework = "net9.0-windows10.0.19041.0"
$runtimeIdentifier = "win-x64"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Tarteel Windows Runner  [$Configuration]" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $projectPath)) {
    Write-Host "Error: Project file not found: $projectPath" -ForegroundColor Red
    exit 1
}

if ($Clean) {
    Write-Host "`n[1/2] Cleaning Windows build..." -ForegroundColor Yellow
    dotnet clean $projectPath -f $targetFramework -c $Configuration --nologo
}

if ($Publish) {
    Write-Host "`nPublishing standalone Windows package..." -ForegroundColor Yellow
    $publishScript = Join-Path $repoRoot "scripts\release\publish-and-run.ps1"
    if (Test-Path $publishScript) {
        & $publishScript -Configuration $Configuration
        exit $LASTEXITCODE
    }
}

Write-Host "`nBuilding and launching Windows app..." -ForegroundColor Yellow

$buildArgs = @(
    "build",
    $projectPath,
    "-f", $targetFramework,
    "-c", $Configuration,
    "-r", $runtimeIdentifier,
    "-p:WindowsPackageType=None",
    "-p:GenerateAppxPackageOnBuild=false",
    "--nologo"
)

& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild FAILED (exit code: $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Locate executable
$binDir = Join-Path $repoRoot "mobile\TarteelMobile\bin\$Configuration\$targetFramework\$runtimeIdentifier"
$exePath = Join-Path $binDir "TarteelMobile.exe"

if (-not (Test-Path $exePath)) {
    # Fallback search
    $exePath = Get-ChildItem -Path (Join-Path $repoRoot "mobile\TarteelMobile\bin\$Configuration\$targetFramework") -Filter "TarteelMobile.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
}

if ($exePath -and (Test-Path $exePath)) {
    Write-Host "`nLaunching: $exePath" -ForegroundColor Green
    Start-Process -FilePath $exePath
} else {
    Write-Host "`nRunning via dotnet run..." -ForegroundColor Yellow
    dotnet run --project $projectPath -f $targetFramework -c $Configuration --no-build
}
