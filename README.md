# 🕌 Tarteel Desktop (Offline-First)

Windows-first offline Quran recitation assistant built with .NET MAUI.
The app captures mic audio, performs on-device ASR with Whisper.net, matches verses locally via Needleman-Wunsch alignment, and stores progress in local SQLite.

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                        .NET MAUI Desktop App                         │
│  ┌─────────────────────┐  ┌──────────────────┐  ┌────────────────┐  │
│  │  RecitationViewModel│  │  ProgressViewModel│  │ AudioService   │  │
│  │  - Live matching    │  │  - Session history│  │ - Mic capture  │  │
│  │  - Confidence dedup │  │  - Stats tracking │  │ - 5s chunks   │  │
│  │  - Green/red markup │  │  - SQLite persist │  │ - 1s overlap  │  │
│  └────────┬────────────┘  └────────┬─────────┘  └───────┬────────┘  │
└───────────┼────────────────────────┼────────────────────┼───────────┘
            │                        │                    │
┌───────────▼────────────────────────▼────────────────────▼───────────┐
│                      Local Recitation Core                           │
│  ┌──────────────────────────┐  ┌──────────────────────────────────┐ │
│  │   PlaceholderVerseMatcher │  │     VerseRepository + Models     │ │
│  │   - NW token alignment    │  │  - SQLite-backed verse lookups   │ │
│  │   - Arabic normalization  │  │  - JSON import fallback (7 seed) │ │
│  │   - Segment probability   │  │  - Full 6236-verse dataset       │ │
│  │   - Tajweed violation det │  │  - Surah/Ayah range queries      │ │
│  └──────────────┬───────────┘  └──────────────────────────────────┘ │
└─────────────────┼───────────────────────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────────────────────┐
│                    LocalWhisperAsrEngine                              │
│  ┌──────────────────────────────┐  ┌──────────────────────────────┐ │
│  │  Whisper.net CPP runtime     │  │  Inference tuning            │ │
│  │  - ggml-base/small/medium    │  │  - BeamSearchWidth: 8        │ │
│  │  - Auto-detect local models  │  │  - Temperature: 0.0          │ │
│  │  - Tier fallback (base→sm)   │  │  - NoSpeechThreshold: 0.6    │ │
│  │  - Warmup on startup         │  │  - EntropyThreshold: 2.4     │ │
│  │  - Segment-level probability │  │  - ArabicNormalizer applied  │ │
│  └──────────────────────────────┘  └──────────────────────────────┘ │
│  ┌──────────────────────────────────────────────────────────────────┐
│  │  Cross-chunk context: last 5 words of previous transcription     │
│  │  passed as prompt to next chunk for continuity across boundaries  │
│  └──────────────────────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────────────┘
```

### Pipeline Flow

```
Microphone ──► AudioService ──► LocalWhisperAsrEngine ──► PlaceholderVerseMatcher
                   │                      │                        │
              5s chunks              Whisper.net               NW alignment
              1s overlap          real segment prob         confidence score
                                  Arabic normalization      token similarity
                                  cross-chunk prompt        tajweed checks
                                  surah prompt                   │
                                      │                          │
                                      └────────┬─────────────────┘
                                               │
                                        RecitationViewModel
                                               │
                                    confidence-based dedup
                                    green/red word markup
```

### Surah Context

When the user selects a surah before recording, the app passes that surah's identity through the pipeline for tighter accuracy:

- **Whisper Prompt** — The surah's Arabic name (e.g. `الفاتحة`) is sent as a `WithPrompt` hint to the Whisper engine, biasing transcription toward expected vocabulary.
- **Verse Matching** — `PlaceholderVerseMatcher` constrains its candidate search to the selected surah's verse range and applies tightened Needleman-Wunsch thresholds: higher minimum token similarity (0.60→0.72 for short words, 0.50→0.60 otherwise), stronger gap penalties (−0.5→−1.2), and raised LCS ratios (0.62→0.75 for tokens ≤4). This eliminates false cross-surah matches when reciting from a known surah.

**Flow:** `ViewModel.SelectedSurah` → `RecitationService.SetSurahContext(surahNum, arabicName)` → `LocalWhisperAsrEngine.SetSurahPrompt(name)` + `OfflineRecitationOrchestrator.SetSurahContext(num)` → `PlaceholderVerseMatcher.SetSurahContext(verseStart, verseEnd)`. Cleared on recording stop.

### Key Design Decisions

**Needleman-Wunsch Token Alignment**
Replaced greedy `CompareTokens` with Needleman-Wunsch sequence alignment for verse
matching. Uses `WordSimilarity` with character-level scoring and Arabic prefix
normalization, plus `GapExtendPenalty` for insertions/deletions. This handles
substitutions, extra words (insertions), and missing words (deletions) far more
robustly than the previous greedy lookahead approach.

**Real Segment Probability**
Confidence is now the average probability across Whisper segments — not hardcoded
0.75. The ViewModel uses this via `ShouldApplyMatchResult` tiebreakers: an incoming
result only replaces the current best when it has better confidence with margin
(>0.03 above) or tolerates small drops (≥best-0.05) when word counts also improve.

**Cross-Chunk Context**
After each successful transcription, the last 5 words are captured and passed as a
`WithPrompt` to the next chunk's Whisper builder. This reduces word fragmentation at
audio chunk boundaries — especially important for Arabic recitation with elongated
vowels (madd).

**Arabic Normalization**
Whisper output is normalized via `ArabicNormalizer` before matching. Handles common
variations in Unicode diacritic representation, tatweel, and punctuation that
otherwise cause false mismatches.

**Inference Tuning**
Beam search (width 8), temperature 0.0 (greedy), adjustable thread count, no-speech
and entropy thresholds all exposed via `LocalWhisperOptions` and `appsettings.json`.
Default fallback tier upgraded from base→base to base→small. Warmup runs on startup
so the first real chunk is responsive.

**Audio Overlap**
Overlap increased from 500ms to 1000ms between 5s chunks. This better accommodates
Arabic madd (elongated vowels) that span chunk boundaries — a ~20% overlap is safe
for CPU inference.

---

## 📁 Repository Structure

```
tarteel-clone/
├── TarteelClone.slnx
├── README.md
├── mobile/
│   └── TarteelMobile/                    # .NET MAUI Windows desktop app
│       ├── ViewModels/                   # RecitationViewModel, ProgressViewModel
│       ├── Views/                        # XAML pages
│       ├── Services/
│       │   ├── Asr/
│       │   │   ├── LocalWhisperAsrEngine.cs    # Whisper.net integration
│       │   │   └── LocalWhisperOptions.cs      # Configuration model
│       │   └── AudioService.cs                 # Mic capture + chunking
│       ├── Models/                        # MatchResult, RecitationWordMismatch, etc.
│       └── Resources/Raw/
│           ├── appsettings.json           # Whisper config + model paths
│           └── quran/import/              # Quran dataset seed + full import
├── src/
│   └── LocalRecitationCore/
│       ├── Services/
│       │   ├── PlaceholderVerseMatcher.cs # NW alignment + confidence scoring
│       │   └── VerseRepository.cs         # SQLite verse storage
│       ├── Models/                        # Verse, MatchResult, WordToken
│       └── Utilities/                     # ArabicNormalizer
├── offline-assets/
│   ├── models/                            # ggml-base.bin, ggml-small.bin
│   ├── data/quran/                        # full_quran.json
│   └── extras/                            # whisper-cli.exe + runtime
├── scripts/
│   ├── release/                           # Windows MSIX packaging
│   └── fetch-full-quran.ps1               # Dataset download
└── .github/workflows/                     # CI (packaging)
```

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- MAUI workload for Windows
- Windows 10/11

### 1) Place offline runtime assets

- `offline-assets/extras/whisper-cli.exe`
- `offline-assets/models/ggml-small.bin`
- optional: `offline-assets/models/ggml-medium.bin`

### 1b) Download the full Quran dataset

The app ships with a 7-verse seed fallback. To load all 6236 verses:

```powershell
pwsh ./scripts/fetch-full-quran.ps1
```

This writes `offline-assets/data/quran/import/full_quran.json`.
The app detects it automatically on next launch and re-imports.

### 2) Install MAUI workload

```powershell
dotnet workload install maui
```

### 3) Build

```powershell
dotnet restore mobile/TarteelMobile/TarteelMobile.csproj `
  -p:TargetFramework=net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64

dotnet build mobile/TarteelMobile/TarteelMobile.csproj `
  -f net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64
```

### 4) Run

```powershell
dotnet run --project mobile/TarteelMobile/TarteelMobile.csproj `
  -f net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifier=win-x64
```

---

## 🪟 Windows Packaging

Scripts under `scripts/release/`:

- `windows-package.ps1` — MSIX packaging
- `windows-packaging.config.example.json` — config template
- `README.windows-packaging.md` — detailed guide
- `.github/workflows/windows-packaging-release.yml` — CI workflow

Build-only validation (no signing):
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -BuildOnly
```

Publish + MSIX (unsigned):
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1
```

---

## 📜 License

MIT — see [LICENSE](LICENSE).
