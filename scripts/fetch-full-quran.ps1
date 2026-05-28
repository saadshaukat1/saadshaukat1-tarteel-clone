#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downloads all 6236 Quran verses from api.quran.com and writes
    offline-assets/data/quran/import/full_quran.json in the format
    expected by LocalVerseRepository.

.NOTES
    Requires PowerShell 7+ and internet access.
    Run once; the app picks up the file on next launch automatically.

.EXAMPLE
    pwsh ./scripts/fetch-full-quran.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$OutputDir  = Join-Path $PSScriptRoot '..\offline-assets\data\quran\import'
$OutputFile = Join-Path $OutputDir 'full_quran.json'
$ApiBase    = 'https://api.quran.com/api/v4'
$Language   = 'en'
$TranslationId = 131   # Saheeh International (en)

$TotalSurahs = 114

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Write-Host "Output: $OutputFile"

$allVerses = [System.Collections.Generic.List[object]]::new()

for ($surah = 1; $surah -le $TotalSurahs; $surah++) {
    $url = "$ApiBase/verses/by_chapter/$surah" +
           "?language=$Language" +
           "&translations=$TranslationId" +
           "&fields=text_uthmani" +
           "&per_page=300"

    Write-Host -NoNewline "Fetching surah $surah/$TotalSurahs ... "

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    }
    catch {
        Write-Warning "Failed to fetch surah $surah`: $_"
        continue
    }

    foreach ($verse in $response.verses) {
        # verse_key is "1:1" format
        $verseKey = $verse.PSObject.Properties['verse_key']?.Value ?? ''
        $parts    = $verseKey -split ':'
        $surahNum = [int]$parts[0]
        $ayahNum  = [int]$parts[1]
        $uthmani  = $verse.PSObject.Properties['text_uthmani']?.Value ?? ''

        $translations = @()
        $verseTranslations = $verse.PSObject.Properties['translations']?.Value
        if ($verseTranslations) {
            foreach ($t in $verseTranslations) {
                # Strip HTML tags that quran.com sometimes embeds in translation text
                $rawText   = $t.PSObject.Properties['text']?.Value ?? ''
                $cleanText = $rawText -replace '<[^>]+>', '' -replace '&amp;', '&' -replace '&lt;', '<' -replace '&gt;', '>'
                $translations += @{
                    language   = $Language
                    text       = $cleanText.Trim()
                    translator = 'Saheeh International'
                }
            }
        }

        $allVerses.Add(@{
            surah_num    = $surahNum
            ayah_num     = $ayahNum
            arabic_text  = $uthmani
            uthmani_text = $uthmani
            translations = $translations
        })
    }

    Write-Host "$($response.verses.Count) verse(s)"
    Start-Sleep -Milliseconds 120   # be polite to the API
}

$output = @{
    source      = 'api.quran.com/v4'
    generatedAt = (Get-Date -Format 'o')
    verses      = $allVerses.ToArray()
}

$json = $output | ConvertTo-Json -Depth 10 -Compress:$false
[System.IO.File]::WriteAllText($OutputFile, $json, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "Done. Wrote $($allVerses.Count) verse(s) to:"
Write-Host "  $OutputFile"
