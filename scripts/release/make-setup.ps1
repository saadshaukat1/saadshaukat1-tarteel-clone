<#
.SYNOPSIS
    Builds a ready-to-ship portable Windows release of Tarteel (self-contained
    publish + offline assets + zip).

.DESCRIPTION
    Publishes TarteelMobile as a self-contained win-x64 build with the offline
    assets embedded, assembles a portable release folder under
    artifacts/setup/Tarteel, and zips it to artifacts/setup/Tarteel-<version>-win-x64.zip.
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = "mobile/TarteelMobile/TarteelMobile.csproj",
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net9.0-windows10.0.19041.0",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "1.0.0",
    [string]$OutputRoot = "artifacts/setup",
    [string]$ModelDir = "offline-assets/models",
    [string]$DataDir = "offline-assets/data",
    [string]$ExtrasDir = "offline-assets/extras",
    [switch]$PushRelease = $true,
    [switch]$NoPush,
    [string]$ReleaseTag = "apk-latest"
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $repoRoot

$projectFullPath = Join-Path $repoRoot $ProjectPath
$outputRootFullPath = Join-Path $repoRoot $OutputRoot
$appDir = Join-Path $outputRootFullPath "Tarteel"
$publishDir = Join-Path $outputRootFullPath "publish"
$zipPath = Join-Path $outputRootFullPath "Tarteel-$Version-win-x64.zip"

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

Write-Host "=== Tarteel Setup Package ==="
Write-Host "Version:   $Version"
Write-Host "Framework: $TargetFramework ($RuntimeIdentifier)"
Write-Host "Output:    $outputRootFullPath"

# ── 1. Publish (self-contained, offline assets embedded) ─────────────────────
New-Item -ItemType Directory -Force -Path $outputRootFullPath | Out-Null
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -Recurse -Force -LiteralPath $publishDir
}

Write-Host ""
Write-Host "=== Publishing (self-contained) ==="
$publishArgs = @(
    "publish",
    $projectFullPath,
    "-c", $Configuration,
    "-f", $TargetFramework,
    "-r", $RuntimeIdentifier,
    "--self-contained",
    "-p:OfflineModelDir=$(Join-Path $repoRoot $ModelDir)",
    "-p:OfflineDataDir=$(Join-Path $repoRoot $DataDir)",
    "-p:OfflineExtrasDir=$(Join-Path $repoRoot $ExtrasDir)",
    "-p:WindowsOnly=true",
    "-p:AppxBundle=Never",
    "-p:WindowsPackageType=None",
    "-p:GenerateAppxPackageOnBuild=false",
    "-p:ApplicationVersion=$Version",
    "-p:ApplicationDisplayVersion=$Version",
    "-p:PublishDir=$publishDir\"
)
Write-Host ">> dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $publishDir "TarteelMobile.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    $exePath = Get-ChildItem -Path $publishDir -Filter "*.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
    if (-not $exePath) {
        throw "Executable not found in publish output: $publishDir"
    }
}

# ── 2. Assemble the release folder ───────────────────────────────────────────
if (Test-Path -LiteralPath $appDir) {
    Remove-Item -Recurse -Force -LiteralPath $appDir
}
New-Item -ItemType Directory -Force -Path $appDir | Out-Null

Write-Host ""
Write-Host "=== Assembling release folder ==="
Copy-Item -Path (Join-Path $publishDir "*") -Destination $appDir -Recurse -Force

# Provide Tarteel.exe alias so users can launch via either name
$srcExe = Join-Path $appDir "TarteelMobile.exe"
$targetExe = Join-Path $appDir "Tarteel.exe"
if (Test-Path $srcExe) {
    Copy-Item $srcExe $targetExe -Force
}

# Trim satellite culture folders — keep only the app's working languages so
# the package stays small. .NET satellite assemblies (zh-CN, de-DE, ...) and
# WinAppSDK locale resources are not needed for an en/ar/ur deployment.
$keepCultures = @("en", "en-us", "ar", "ar-sa", "ur", "ur-pk")
$removedCultures = 0
foreach ($dir in Get-ChildItem -Path $appDir -Directory) {
    if ($dir.Name -match "^[a-z]{2}(-[a-z0-9]+)?$" -and $keepCultures -notcontains $dir.Name.ToLowerInvariant()) {
        Remove-Item -Recurse -Force -LiteralPath $dir.FullName
        $removedCultures++
    }
}
Write-Host "Removed $removedCultures satellite culture folder(s)."

# Offline assets: the app's asset-extraction fallback reads these at runtime.
foreach ($asset in @($ModelDir, $DataDir, $ExtrasDir)) {
    $source = Join-Path $repoRoot $asset
    if (Test-Path -LiteralPath $source) {
        $target = Join-Path $appDir $asset
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        if ($asset -eq $ModelDir) {
            # Models (base and tiny) are already embedded in offline/models via MAUI assets; no duplicate copy needed
            Write-Host "Default offline models (base & tiny) are embedded directly in package."
        } else {
            Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
            Write-Host "Copied $asset -> $target"
        }
    } else {
        Write-Warning "Offline asset folder not found, skipped: $source"
    }
}

# Clean temporary publishDir immediately to free disk space
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -Recurse -Force -LiteralPath $publishDir
}

# ── 3. README inside the package ─────────────────────────────────────────────
$readme = @"
Tarteel - Offline Quran recitation assistant
Version $Version

HOW TO RUN
  1. Extract this folder anywhere (e.g. C:\Tarteel).
  2. Run Tarteel.exe.

SYSTEM REQUIREMENTS
  - Windows 10 version 2004 (19041) or later, or Windows 11
  - 64-bit (x64)
  - Microphone for recitation practice
  - ~1 GB free disk space (Whisper models + Quran dataset included)

OFFLINE ASSETS
  - offline-assets/models   Whisper speech models (base/small)
  - offline-assets/data     Full Quran dataset (6,236 verses)
  - offline-assets/extras   Whisper native runtime libraries

USER DATA
  Progress, profiles, streaks and logs are stored under:
    %LOCALAPPDATA%\TarteelClone
    %LOCALAPPDATA%\Tarteel

NOTE
  This build is unsigned. Windows SmartScreen may warn on first launch;
  choose "More info" then "Run anyway".
"@
Set-Content -Path (Join-Path $appDir "README.txt") -Value $readme -Encoding UTF8

# ── 4. Zip (Direct streaming, no temp disk buffer) ───────────────────────────
if (Test-Path -LiteralPath $zipPath) {
    $removed = $false
    for ($i = 0; $i -lt 5; $i++) {
        try {
            Remove-Item -Force -LiteralPath $zipPath -ErrorAction Stop
            $removed = $true
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    if (-not $removed) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $zipPath = Join-Path $outputRootFullPath "Tarteel-$Version-$timestamp-win-x64.zip"
        Write-Host "Notice: Existing package is locked by another process. Writing to $zipPath instead." -ForegroundColor Yellow
    }
}
Write-Host ""
Write-Host "=== Zipping package directly ==="
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($appDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$sizeMb = [Math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "=== Complete ==="
Write-Host "Release folder: $appDir"
Write-Host "Package:        $zipPath ($sizeMb MB)"

# ── 5. Push to GitHub Releases ───────────────────────────────────────────────
if ($PushRelease -and -not $NoPush) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Pushing Windows Package to GitHub Releases" -ForegroundColor Cyan
    Write-Host "  Tag     : $ReleaseTag"
    Write-Host "  Package : $zipPath"
    Write-Host "========================================" -ForegroundColor Cyan

    $gh = Get-Command "gh" -ErrorAction SilentlyContinue
    if (-not $gh) {
        Write-Host "Warning: GitHub CLI ('gh') not found. Skipping release upload." -ForegroundColor Yellow
    } else {
        $existingReleases = @(& gh release list --limit 100 --json tagName -q ".[].tagName")
        $releaseExists = ($existingReleases -contains $ReleaseTag)
        $zipFileName = Split-Path $zipPath -Leaf

        if (-not $releaseExists) {
            Write-Host "Creating release '$ReleaseTag'..." -ForegroundColor Yellow
            $title = "Tarteel Windows Desktop (v$Version)"
            $notes = "Automated Windows release (v$Version - win-x64 self-contained portable)"
            & gh release create $ReleaseTag $zipPath --title $title --notes $notes
        } else {
            Write-Host "Release '$ReleaseTag' exists. Uploading $zipFileName..." -ForegroundColor Yellow
            & gh release upload $ReleaseTag $zipPath --clobber
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Host "`nSuccessfully uploaded $zipFileName to release '$ReleaseTag'!" -ForegroundColor Green
            Write-Host "Download URL: https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/$ReleaseTag/$zipFileName" -ForegroundColor Cyan
        } else {
            Write-Host "`nFailed to upload Windows release to '$ReleaseTag' (exit code $LASTEXITCODE)." -ForegroundColor Red
        }
    }
}
