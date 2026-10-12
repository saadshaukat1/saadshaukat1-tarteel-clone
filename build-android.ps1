# build-android.ps1 — Build, deploy, release and tail logcat for the Tarteel Android app.
# Usage: .\build-android.ps1 [-Clean] [-Release] [-NoInstall] [-PushRelease] [-NoPush] [-ReleaseTag <tag>] [-LogTag <tag>]
param(
    [switch]$Clean,
    [switch]$Release,
    [switch]$NoInstall,
    [switch]$PushRelease = $true,
    [switch]$NoPush,
    [string]$ReleaseTag = "apk-latest",
    [string]$LogTag = "DOTNET,AndroidRuntime,mono-rt,TarteelMobile"
)

$ErrorActionPreference = "Stop"

$JAVA_SDK   = "D:\jdk17\jdk-17.0.20+8"
$ANDROID_SDK = "D:\Android\Sdk"
$ADB        = "$ANDROID_SDK\platform-tools\adb.exe"
$PROJ       = "$PSScriptRoot\mobile\TarteelMobile\TarteelMobile.csproj"
$CONFIG     = if ($Release) { "Release" } else { "Debug" }

$env:PATH = "$ANDROID_SDK\platform-tools;$env:PATH"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Tarteel Android Build  [$CONFIG]" -ForegroundColor Cyan
Write-Host "  JDK  : $JAVA_SDK"
Write-Host "  SDK  : $ANDROID_SDK"
Write-Host "========================================" -ForegroundColor Cyan

if ($Clean) {
    Write-Host "`n[1/3] Cleaning..." -ForegroundColor Yellow
    dotnet clean $PROJ -f net9.0-android --nologo `
      -p:JavaSdkDirectory=$JAVA_SDK `
      -p:AndroidSdkDirectory=$ANDROID_SDK
}

Write-Host "`n[2/3] Building..." -ForegroundColor Yellow
dotnet build $PROJ `
  -f net9.0-android `
  -c $CONFIG `
  -p:JavaSdkDirectory=$JAVA_SDK `
  -p:AndroidSdkDirectory=$ANDROID_SDK `
  -p:AdbTarget="" `
  --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nBuild succeeded." -ForegroundColor Green

# Locate generated APK
$apk = "$PSScriptRoot\mobile\TarteelMobile\bin\$CONFIG\net9.0-android\com.tarteel.mobile-Signed.apk"
if (-not (Test-Path $apk)) {
    $apk = "$PSScriptRoot\mobile\TarteelMobile\bin\$CONFIG\net9.0-android\com.tarteel.mobile.apk"
}
if (-not (Test-Path $apk)) {
    $apk = Get-ChildItem -Path "$PSScriptRoot\mobile\TarteelMobile\bin\$CONFIG\net9.0-android" -Filter "*.apk" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
}

# Push APK to GitHub Releases
if ($PushRelease -and -not $NoPush) {
    $pushScript = Join-Path $PSScriptRoot "scripts\release\push-apk-release.ps1"
    if (Test-Path $pushScript) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $pushScript -ApkPath $apk -ReleaseTag $ReleaseTag -Configuration $CONFIG
    }
}

if (-not $NoInstall) {
    Write-Host "`nInstalling APK to device and tailing logcat (Ctrl+C to stop)..." -ForegroundColor Yellow
    Write-Host "      Installing: $apk" -ForegroundColor DarkGray
    & $ADB install -r $apk
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Install failed (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "      Installed successfully. Launching app..." -ForegroundColor Green
    & $ADB logcat -c
    & $ADB shell am start -n "com.tarteel.mobile/crc647e850992537f7714.MainActivity"
    Write-Host "      Tags: $LogTag" -ForegroundColor DarkGray
    & $ADB logcat -s $LogTag
}
