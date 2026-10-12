<#
.SYNOPSIS
Downloads quantized Whisper GGML models (including large-v3-turbo-q5_0) for local recitation.

.DESCRIPTION
Downloads high-efficiency quantized Whisper models directly from the official GGML repository on Hugging Face.
Supports:
  - turbo   : ggml-large-v3-turbo-q5_0.bin (~547 MiB) - Near-Large-v3 accuracy at Small model speeds.
  - small   : ggml-small-q5_1.bin (~182 MiB) - Balanced accuracy for high-end mobile and laptops.
  - base    : ggml-base-q5_1.bin (~57 MiB) - Fast standard tier for mobile phones and laptops.
  - tiny    : ggml-tiny-q5_1.bin (~31 MiB) - Lightweight model for low-spec Android devices.
  - all     : Downloads all models listed above.

.EXAMPLE
.\scripts\download_models.ps1 -Tier turbo
.\scripts\download_models.ps1 -Tier base,turbo
.\scripts\download_models.ps1 -Tier all
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string[]]$Tier = @("turbo", "base"),

    [Parameter()]
    [string]$TargetDir = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path (Get-Location).Path "offline-assets\models"
}
$TargetDir = [System.IO.Path]::GetFullPath($TargetDir)

$ModelCatalog = @{
    "tiny" = @{
        FileName = "ggml-tiny-q5_1.bin"
        Url = "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/models-v1/ggml-tiny-q5_1.bin"
        SizeMb = 31
        Description = "Tiny Q5_1 (Ultra-light, ~31MB)"
    }
    "base" = @{
        FileName = "ggml-base-q5_1.bin"
        Url = "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/models-v1/ggml-base-q5_1.bin"
        SizeMb = 57
        Description = "Base Q5_1 (Fast mobile default, ~57MB)"
    }
    "small" = @{
        FileName = "ggml-small-q5_1.bin"
        Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin?download=true"
        SizeMb = 182
        Description = "Small Q5_1 (High-accuracy phonemes, ~182MB)"
    }
    "medium" = @{
        FileName = "ggml-medium-q5_0.bin"
        Url = "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/models-v1/ggml-medium-q5_0.bin"
        SizeMb = 515
        Description = "Medium Q5_0 (High accuracy, ~515MB)"
    }
    "turbo" = @{
        FileName = "ggml-large-v3-turbo-q5_0.bin"
        Url = "https://github.com/saadshaukat1/saadshaukat1-tarteel-clone/releases/download/models-v1/ggml-large-v3-turbo-q5_0.bin"
        SizeMb = 547
        Description = "Large-v3-Turbo Q5_0 (Flagship accuracy at fast speeds, ~547MB)"
    }
}

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "[+] Created target directory: $TargetDir" -ForegroundColor Cyan
}

$SelectedTiers = @()
if ($Tier -contains "all") {
    $SelectedTiers = @("tiny", "base", "small", "medium", "turbo")
} else {
    foreach ($entry in $Tier) {
        foreach ($t in ($entry -split ",")) {
            $clean = $t.Trim().ToLowerInvariant()
            if ($clean -eq "all") {
                $SelectedTiers = @("tiny", "base", "small", "medium", "turbo")
                break
            }
            if ($ModelCatalog.ContainsKey($clean)) {
                $SelectedTiers += $clean
            } elseif (-not [string]::IsNullOrWhiteSpace($clean)) {
                Write-Warning "Unknown tier '$t'. Available: tiny, base, small, medium, turbo, all"
            }
        }
    }
}

if ($SelectedTiers.Count -eq 0) {
    Write-Error "No valid tiers specified."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Tarteel Model Downloader (Quantized GGML Models)" -ForegroundColor Green
Write-Host " Target Directory: $TargetDir" -ForegroundColor Green
Write-Host " Selected: $($SelectedTiers -join ', ')" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromMinutes(30)

foreach ($tierKey in $SelectedTiers) {
    $info = $ModelCatalog[$tierKey]
    $destPath = Join-Path $TargetDir $info.FileName
    $tempPath = "$destPath.tmp"

    Write-Host ""
    Write-Host "[*] Tier: $($info.Description)" -ForegroundColor Yellow
    Write-Host "    URL: $($info.Url)"

    if (Test-Path $destPath) {
        $existingSize = (Get-Item $destPath).Length
        if ($existingSize -ge ($info.SizeMb * 1024 * 1024 * 0.9)) {
            Write-Host "    [OK] Model already present ($([math]::Round($existingSize / 1MB, 1)) MB). Skipping." -ForegroundColor Green
            continue
        } else {
            Write-Warning "    Incomplete file found ($([math]::Round($existingSize / 1MB, 1)) MB). Re-downloading..."
            Remove-Item $destPath -Force
        }
    }

    Write-Host "    Downloading $($info.FileName) (~$($info.SizeMb) MB)..." -ForegroundColor Cyan

    try {
        $response = $client.GetAsync($info.Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            Write-Error "Failed to download $($info.FileName): HTTP $($response.StatusCode)"
            continue
        }

        $totalBytes = $response.Content.Headers.ContentLength
        $sourceStream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $destStream = [System.IO.File]::Create($tempPath)

        $buffer = New-Object byte[] 65536
        $totalRead = 0
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $lastReport = 0

        while (($read = $sourceStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $destStream.Write($buffer, 0, $read)
            $totalRead += $read

            if ($sw.ElapsedMilliseconds - $lastReport -gt 1000) {
                $lastReport = $sw.ElapsedMilliseconds
                $mbRead = [math]::Round($totalRead / 1MB, 1)
                $speedMb = [math]::Round(($totalRead / 1MB) / ($sw.Elapsed.TotalSeconds + 0.001), 2)
                if ($totalBytes) {
                    $pct = [math]::Round(($totalRead / $totalBytes) * 100, 1)
                    Write-Progress -Activity "Downloading $($info.FileName)" -Status "$mbRead MB of $([math]::Round($totalBytes / 1MB, 1)) MB ($speedMb MB/s)" -PercentComplete $pct
                } else {
                    Write-Progress -Activity "Downloading $($info.FileName)" -Status "$mbRead MB downloaded ($speedMb MB/s)"
                }
            }
        }

        $destStream.Close()
        $sourceStream.Close()
        Write-Progress -Activity "Downloading $($info.FileName)" -Completed

        Move-Item -Path $tempPath -Destination $destPath -Force
        $sw.Stop()

        $finalMb = [math]::Round((Get-Item $destPath).Length / 1MB, 1)
        Write-Host "    [SUCCESS] Downloaded $($info.FileName) ($finalMb MB in $([math]::Round($sw.Elapsed.TotalSeconds, 1))s)" -ForegroundColor Green
    }
    catch {
        Write-Error "Error downloading $($info.FileName): $_"
        if (Test-Path $tempPath) { Remove-Item $tempPath -Force }
    }
}

$client.Dispose()
Write-Host ""
Write-Host "[+] Download operations completed." -ForegroundColor Green
