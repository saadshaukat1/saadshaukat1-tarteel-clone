# run-android.ps1 — Build, install, and run Tarteel MAUI on Android
# Usage:
#   .\run-android.ps1 [-Clean] [-Release] [-NoInstall] [-LogTag <tag>] [-AndroidSdk <path>] [-JavaSdk <path>]
param(
    [switch]$Clean,
    [switch]$Release,
    [switch]$NoInstall,
    [string]$LogTag = "DOTNET,AndroidRuntime,mono-rt,TarteelMobile",
    [string]$AndroidSdk = "",
    [string]$JavaSdk = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
Set-Location $repoRoot

# Auto-detect Android SDK
if (-not $AndroidSdk) {
    $candidates = @(
        "D:\Android\Sdk",
        "$env:ANDROID_HOME",
        "$env:ANDROID_SDK_ROOT",
        "$env:LOCALAPPDATA\Android\Sdk"
    )
    foreach ($cand in $candidates) {
        if ($cand -and (Test-Path $cand)) {
            $AndroidSdk = $cand
            break
        }
    }
}

# Auto-detect Java SDK
if (-not $JavaSdk) {
    $jdkCandidates = @(
        "D:\jdk17\jdk-17.0.20+8",
        "$env:JAVA_HOME"
    )
    # Check Android Studio jbr or Program Files OpenJDK / Java
    $androidStudioJbr = "C:\Program Files\Android\Android Studio\jbr"
    if (Test-Path $androidStudioJbr) { $jdkCandidates += $androidStudioJbr }

    $jdkRoots = @("C:\Program Files\Java", "C:\Program Files\Eclipse Adoptium", "C:\Program Files\Microsoft")
    foreach ($r in $jdkRoots) {
        if (Test-Path $r) {
            Get-ChildItem -Path $r -Directory | ForEach-Object { $jdkCandidates += $_.FullName }
        }
    }

    foreach ($cand in $jdkCandidates) {
        if ($cand -and (Test-Path $cand)) {
            $JavaSdk = $cand
            break
        }
    }
}

$ADB = if ($AndroidSdk -and (Test-Path "$AndroidSdk\platform-tools\adb.exe")) {
    "$AndroidSdk\platform-tools\adb.exe"
} else {
    "adb"
}

$PROJ   = Join-Path $repoRoot "mobile\TarteelMobile\TarteelMobile.csproj"
$CONFIG = if ($Release) { "Release" } else { "Debug" }

if ($AndroidSdk) {
    $env:PATH = "$AndroidSdk\platform-tools;$env:PATH"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Tarteel Android Runner  [$CONFIG]" -ForegroundColor Cyan
Write-Host "  JDK  : $(if ($JavaSdk) { $JavaSdk } else { 'Default / Auto' })"
Write-Host "  SDK  : $(if ($AndroidSdk) { $AndroidSdk } else { 'Default / Auto' })"
Write-Host "========================================" -ForegroundColor Cyan

# Check if ADB sees any connected devices or emulators
if (-not $NoInstall) {
    try {
        $devicesOutput = & $ADB devices 2>$null
        $deviceLines = ($devicesOutput | Select-String -Pattern "\bdevice\b" -CaseSensitive)
        if (-not $deviceLines) {
            Write-Host "`nWarning: No active Android device or emulator detected via ADB." -ForegroundColor Yellow
            Write-Host "Make sure your Android device is connected with USB Debugging enabled, or an emulator is running." -ForegroundColor Yellow
        } else {
            Write-Host "`nTarget Device(s) detected:" -ForegroundColor Green
            $deviceLines | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
        }
    } catch {
        Write-Host "`nWarning: ADB command could not be checked." -ForegroundColor Yellow
    }
}

$extraMsBuildProps = @()
if ($JavaSdk) {
    $extraMsBuildProps += "-p:JavaSdkDirectory=$JavaSdk"
}
if ($AndroidSdk) {
    $extraMsBuildProps += "-p:AndroidSdkDirectory=$AndroidSdk"
}

if ($Clean) {
    Write-Host "`n[1/3] Cleaning..." -ForegroundColor Yellow
    dotnet clean $PROJ -f net9.0-android --nologo @extraMsBuildProps
}

Write-Host "`n[2/3] Building APK..." -ForegroundColor Yellow
dotnet build $PROJ `
  -f net9.0-android `
  -c $CONFIG `
  -p:AdbTarget="" `
  @extraMsBuildProps `
  --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild FAILED (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nBuild succeeded." -ForegroundColor Green

if (-not $NoInstall) {
    Write-Host "`n[3/3] Installing APK to device and tailing logcat (Ctrl+C to stop)..." -ForegroundColor Yellow
    $apk = Join-Path $repoRoot "mobile\TarteelMobile\bin\$CONFIG\net9.0-android\com.tarteel.mobile-Signed.apk"
    if (-not (Test-Path $apk)) {
        $apk = Join-Path $repoRoot "mobile\TarteelMobile\bin\$CONFIG\net9.0-android\com.tarteel.mobile.apk"
    }
    if (-not (Test-Path $apk)) {
        # Search fallback
        $apk = Get-ChildItem -Path (Join-Path $repoRoot "mobile\TarteelMobile\bin\$CONFIG\net9.0-android") -Filter "*.apk" | Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $apk -or -not (Test-Path $apk)) {
        Write-Host "Could not find generated APK in bin directory." -ForegroundColor Red
        exit 1
    }

    Write-Host "      Installing: $apk" -ForegroundColor DarkGray
    & $ADB install -r $apk
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Install failed (exit code $LASTEXITCODE). Ensure an Android device/emulator is running." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "      Installed successfully. Launching app..." -ForegroundColor Green
    & $ADB logcat -c
    & $ADB shell am start -n "com.tarteel.mobile/crc647e850992537f7714.MainActivity"
    Write-Host "      Tailing logcat tags: $LogTag" -ForegroundColor DarkGray
    & $ADB logcat -s $LogTag
}
