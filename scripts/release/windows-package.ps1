[CmdletBinding()]
param(
    [string]$ProjectPath = "mobile/TarteelMobile/TarteelMobile.csproj",
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net10.0-windows10.0.19041.0",
    [string]$RuntimeIdentifier = "win10-x64",
    [string]$OutputRoot = "artifacts/windows-packaging",
    [string]$ModelDir = "",
    [string]$DataDir = "",
    [string]$ExtrasDir = "",
    [string]$Version = "",
    [string]$DisplayVersion = "",
    [switch]$BuildOnly,
    [switch]$DryRun,
    [switch]$SkipRestore,
    [switch]$SelfContained,
    [string]$ConfigPath = "",
    [string]$CertPath = "",
    [string]$CertThumbprint = "",
    [string]$CertPassword = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$script:InvocationBoundKeys = @{}
foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $script:InvocationBoundKeys[$entry.Key] = $true
}
$buildOnlyEnabled = $BuildOnly.IsPresent
$dryRunEnabled = $DryRun.IsPresent
$selfContainedEnabled = $SelfContained.IsPresent

function Resolve-RepoPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PathValue))
}

function Write-Section {
    param([string]$Message)
    Write-Host ""
    Write-Host "=== $Message ==="
}

function Format-DotNetCommand {
    param([string[]]$Arguments)

    $parts = @("dotnet")
    foreach ($arg in $Arguments) {
        # Redact certificate password from logged output.
        if ($arg -match "(?i)PackageCertificatePassword=") {
            $parts += "-p:PackageCertificatePassword=***"
            continue
        }
        if ($arg -match "\s") {
            $parts += '"' + ($arg.Replace('"', '\"')) + '"'
        } else {
            $parts += $arg
        }
    }

    return ($parts -join " ")
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    $cmdText = Format-DotNetCommand -Arguments $Arguments
    Write-Host ">> $cmdText"

    if ($script:DryRunEnabled) {
        return
    }

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }
}

function Apply-ConfigValue {
    param(
        [string]$ParameterName,
        [object]$Value
    )

    if ($null -eq $Value -or $script:InvocationBoundKeys.ContainsKey($ParameterName)) {
        return
    }

    Set-Variable -Name $ParameterName -Value $Value -Scope Script
}

Set-Location $repoRoot

if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
    $resolvedConfigPath = Resolve-RepoPath -PathValue $ConfigPath
    if (-not (Test-Path -LiteralPath $resolvedConfigPath)) {
        throw "Config file not found: $resolvedConfigPath"
    }

    Write-Section "Loading Packaging Config"
    Write-Host "Using config file: $resolvedConfigPath"
    $config = Get-Content -Raw -LiteralPath $resolvedConfigPath | ConvertFrom-Json

    Apply-ConfigValue -ParameterName "ProjectPath" -Value $config.projectPath
    Apply-ConfigValue -ParameterName "Configuration" -Value $config.configuration
    Apply-ConfigValue -ParameterName "TargetFramework" -Value $config.targetFramework
    Apply-ConfigValue -ParameterName "RuntimeIdentifier" -Value $config.runtimeIdentifier
    Apply-ConfigValue -ParameterName "OutputRoot" -Value $config.outputRoot
    Apply-ConfigValue -ParameterName "Version" -Value $config.version
    Apply-ConfigValue -ParameterName "DisplayVersion" -Value $config.displayVersion
    Apply-ConfigValue -ParameterName "ModelDir" -Value $config.offlineAssets.modelDir
    Apply-ConfigValue -ParameterName "DataDir" -Value $config.offlineAssets.dataDir
    Apply-ConfigValue -ParameterName "ExtrasDir" -Value $config.offlineAssets.extrasDir
    Apply-ConfigValue -ParameterName "CertPath" -Value $config.signing.certificatePath
    Apply-ConfigValue -ParameterName "CertThumbprint" -Value $config.signing.certificateThumbprint

    if (-not $script:InvocationBoundKeys.ContainsKey("BuildOnly") -and $null -ne $config.buildOnly) {
        $buildOnlyEnabled = [bool]$config.buildOnly
    }
    if (-not $script:InvocationBoundKeys.ContainsKey("DryRun") -and $null -ne $config.dryRun) {
        $dryRunEnabled = [bool]$config.dryRun
    }
    if (-not $script:InvocationBoundKeys.ContainsKey("SelfContained") -and $null -ne $config.selfContained) {
        $selfContainedEnabled = [bool]$config.selfContained
    }
    if (
        -not $script:InvocationBoundKeys.ContainsKey("CertPassword") -and
        -not [string]::IsNullOrWhiteSpace($config.signing.certificatePasswordEnvVar)
    ) {
        $passwordFromEnv = [Environment]::GetEnvironmentVariable([string]$config.signing.certificatePasswordEnvVar)
        if (-not [string]::IsNullOrWhiteSpace($passwordFromEnv)) {
            $CertPassword = $passwordFromEnv
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ModelDir)) {
    $ModelDir = "offline-assets/models"
}
if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = "offline-assets/data"
}
if ([string]::IsNullOrWhiteSpace($ExtrasDir)) {
    $ExtrasDir = "offline-assets/extras"
}
if ([string]::IsNullOrWhiteSpace($CertPassword) -and -not [string]::IsNullOrWhiteSpace($env:WINDOWS_CERT_PASSWORD)) {
    $CertPassword = $env:WINDOWS_CERT_PASSWORD
}

$projectFullPath = Resolve-RepoPath -PathValue $ProjectPath
$outputRootFullPath = Resolve-RepoPath -PathValue $OutputRoot
$modelDirFullPath = Resolve-RepoPath -PathValue $ModelDir
$dataDirFullPath = Resolve-RepoPath -PathValue $DataDir
$extrasDirFullPath = Resolve-RepoPath -PathValue $ExtrasDir
$certPathFullPath = Resolve-RepoPath -PathValue $CertPath

if (-not (Test-Path -LiteralPath $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

if (-not $dryRunEnabled) {
    New-Item -ItemType Directory -Force -Path $outputRootFullPath | Out-Null
}

foreach ($asset in @(
        @{ Name = "ModelDir"; Value = $modelDirFullPath },
        @{ Name = "DataDir"; Value = $dataDirFullPath },
        @{ Name = "ExtrasDir"; Value = $extrasDirFullPath }
    )) {
    if (-not (Test-Path -LiteralPath $asset.Value)) {
        Write-Warning "$($asset.Name) path does not exist and will be skipped by MSBuild: $($asset.Value)"
    }
}

$signingEnabled = $false
if (-not [string]::IsNullOrWhiteSpace($certPathFullPath) -or -not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
    $signingEnabled = $true
}

if ($signingEnabled -and -not [string]::IsNullOrWhiteSpace($certPathFullPath) -and -not (Test-Path -LiteralPath $certPathFullPath)) {
    throw "Certificate file not found: $certPathFullPath"
}

$publishDir = Join-Path $outputRootFullPath "publish"
$msixDir = Join-Path $outputRootFullPath "msix"

$sharedArgs = @(
    $projectFullPath,
    "-c", $Configuration,
    "-f", $TargetFramework,
    "-r", $RuntimeIdentifier,
    "-p:OfflineModelDir=$modelDirFullPath",
    "-p:OfflineDataDir=$dataDirFullPath",
    "-p:OfflineExtrasDir=$extrasDirFullPath",
    "-p:SelfContained=$($selfContainedEnabled.ToString().ToLowerInvariant())",
    "-p:AppxBundle=Never"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $sharedArgs += "-p:ApplicationVersion=$Version"
}

if (-not [string]::IsNullOrWhiteSpace($DisplayVersion)) {
    $sharedArgs += "-p:ApplicationDisplayVersion=$DisplayVersion"
}

if ($signingEnabled) {
    $sharedArgs += "-p:AppxPackageSigningEnabled=true"
    if (-not [string]::IsNullOrWhiteSpace($certPathFullPath)) {
        $sharedArgs += "-p:PackageCertificateKeyFile=$certPathFullPath"
    }
    if (-not [string]::IsNullOrWhiteSpace($CertThumbprint)) {
        $sharedArgs += "-p:PackageCertificateThumbprint=$CertThumbprint"
    }
    # Pass the certificate password through an environment variable so it is never
    # written to the build log or visible in process argument lists.
    if (-not [string]::IsNullOrWhiteSpace($CertPassword)) {
        $env:PackageCertificatePassword = $CertPassword
        $sharedArgs += "-p:PackageCertificatePassword=$($env:PackageCertificatePassword)"
    }
} else {
    $sharedArgs += "-p:AppxPackageSigningEnabled=false"
}

Write-Section "Windows Packaging Inputs"
Write-Host "Project:            $projectFullPath"
Write-Host "Configuration:      $Configuration"
Write-Host "Framework:          $TargetFramework"
Write-Host "Runtime Identifier: $RuntimeIdentifier"
Write-Host "Output Root:        $outputRootFullPath"
Write-Host "Model Dir:          $modelDirFullPath"
Write-Host "Data Dir:           $dataDirFullPath"
Write-Host "Extras Dir:         $extrasDirFullPath"
Write-Host "Signing Enabled:    $signingEnabled"
Write-Host "Build Only:         $buildOnlyEnabled"
Write-Host "Dry Run:            $dryRunEnabled"

if (-not $SkipRestore) {
    Write-Section "Restoring Project"
    Invoke-DotNet -Arguments @(
        "restore",
        $projectFullPath,
        "-r", $RuntimeIdentifier,
        "-p:TargetFramework=$TargetFramework"
    )
}

if ($buildOnlyEnabled) {
    Write-Section "Running Build Validation"
    Invoke-DotNet -Arguments (@(
            "build"
        ) + $sharedArgs + @(
            "-p:GenerateAppxPackageOnBuild=false"
        ))
} else {
    if (-not $dryRunEnabled) {
        New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
        New-Item -ItemType Directory -Force -Path $msixDir | Out-Null
    }

    $publishLifecycleArgs = @()
    if ($selfContainedEnabled) {
        $publishLifecycleArgs += "--self-contained"
    } else {
        $publishLifecycleArgs += "--no-self-contained"
    }

    Write-Section "Running Publish + MSIX Packaging"
    Invoke-DotNet -Arguments (@(
            "publish"
        ) + $sharedArgs + $publishLifecycleArgs + @(
            "-p:WindowsPackageType=MSIX",
            "-p:GenerateAppxPackageOnBuild=true",
            "-p:AppxPackageDir=$msixDir\",
            "-p:PublishDir=$publishDir\"
        ))
}

Write-Section "Complete"
Write-Host "Packaging automation run completed."
