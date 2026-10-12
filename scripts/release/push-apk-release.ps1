# push-apk-release.ps1 — Push built Android APK to GitHub Releases
[CmdletBinding()]
param(
    [string]$ApkPath = "",
    [string]$ReleaseTag = "apk-latest",
    [string]$Configuration = "Debug",
    [string]$Title = "",
    [string]$Notes = ""
)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))

# Auto-locate APK if not provided
if (-not $ApkPath) {
    $candidates = @(
        (Join-Path $repoRoot "mobile\TarteelMobile\bin\$Configuration\net9.0-android\com.tarteel.mobile-Signed.apk"),
        (Join-Path $repoRoot "mobile\TarteelMobile\bin\$Configuration\net9.0-android\com.tarteel.mobile.apk"),
        (Join-Path $repoRoot "mobile\TarteelMobile\bin\Release\net9.0-android\com.tarteel.mobile-Signed.apk"),
        (Join-Path $repoRoot "mobile\TarteelMobile\bin\Debug\net9.0-android\com.tarteel.mobile-Signed.apk")
    )
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $ApkPath = $cand
            break
        }
    }
    if (-not $ApkPath) {
        $found = Get-ChildItem -Path (Join-Path $repoRoot "mobile\TarteelMobile\bin") -Recurse -Filter "*Signed.apk" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { $ApkPath = $found.FullName }
    }
}

if (-not $ApkPath -or -not (Test-Path $ApkPath)) {
    Write-Host "Error: Cannot push to release. APK not found." -ForegroundColor Red
    return
}

$ApkPath = [System.IO.Path]::GetFullPath($ApkPath)
$apkSizeMB = [math]::Round(((Get-Item $ApkPath).Length / 1MB), 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Pushing APK to GitHub Releases" -ForegroundColor Cyan
Write-Host "  Tag     : $ReleaseTag"
Write-Host "  File    : $ApkPath ($apkSizeMB MB)"
Write-Host "========================================" -ForegroundColor Cyan

# Verify GitHub CLI
$gh = Get-Command "gh" -ErrorAction SilentlyContinue
if (-not $gh) {
    Write-Host "Warning: GitHub CLI ('gh') is not installed or not in PATH." -ForegroundColor Yellow
    Write-Host "Install gh or run: winget install GitHub.cli" -ForegroundColor Yellow
    return
}

# Verify gh authentication
$authOut = (& gh auth status 2>&1)
if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: GitHub CLI is not logged in. Run 'gh auth login' to authenticate." -ForegroundColor Yellow
    return
}

# Version & metadata
$version = "1.0"
$projFile = Join-Path $repoRoot "mobile\TarteelMobile\TarteelMobile.csproj"
if (Test-Path $projFile) {
    [xml]$projXml = Get-Content $projFile
    $displayVer = $projXml.Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1
    if ($displayVer) { $version = $displayVer }
}

$commitHash = ""
try { $commitHash = (& git rev-parse --short HEAD 2>$null) } catch {}

$branch = ""
try { $branch = (& git rev-parse --abbrev-ref HEAD 2>$null) } catch {}

$apkFileName = Split-Path $ApkPath -Leaf

if (-not $Title) {
    $Title = "Tarteel Mobile APK (v$version - $Configuration)"
}

if (-not $Notes) {
    $Notes = @"
Automated Android APK build ($Configuration)
- App Version: v$version
- Branch: $branch
- Commit: $commitHash
- Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
- File: $apkFileName ($apkSizeMB MB)
"@
}

# Check if release exists using gh release list
Write-Host "Checking release '$ReleaseTag' on GitHub..." -ForegroundColor DarkGray
$existingReleases = @(& gh release list --limit 100 --json tagName -q ".[].tagName")
$releaseExists = ($existingReleases -contains $ReleaseTag)

if (-not $releaseExists) {
    Write-Host "Creating GitHub release '$ReleaseTag'..." -ForegroundColor Yellow
    $createArgs = @("release", "create", $ReleaseTag, $ApkPath, "--title", $Title, "--notes", $Notes)
    if ($branch) {
        $createArgs += @("--target", $branch)
    }
    & gh @createArgs
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nSuccessfully created release '$ReleaseTag' and published APK!" -ForegroundColor Green
        Write-Host "Download URL: https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/$ReleaseTag/$apkFileName" -ForegroundColor Cyan
    } else {
        Write-Host "`nFailed to create release '$ReleaseTag' (exit code $LASTEXITCODE)." -ForegroundColor Red
    }
} else {
    Write-Host "Release '$ReleaseTag' exists. Uploading APK asset..." -ForegroundColor Yellow
    & gh release upload $ReleaseTag $ApkPath --clobber
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nSuccessfully uploaded $apkFileName to release '$ReleaseTag'!" -ForegroundColor Green
        Write-Host "Download URL: https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/$ReleaseTag/$apkFileName" -ForegroundColor Cyan
    } else {
        Write-Host "`nFailed to upload APK to release '$ReleaseTag' (exit code $LASTEXITCODE)." -ForegroundColor Red
    }
}
