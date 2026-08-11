# AI Guidance for Tarteel Clone

This document is a practical knowledge base for AI agents and contributors working in this repository.

It prioritizes code-verified behavior over historical notes.

## 1. Project Identity

- Name: Tarteel desktop clone (offline-first prototype).
- Primary app: .NET MAUI Windows app in mobile/TarteelMobile.
- Main goal: local microphone recitation flow with local ASR and local Quran data.
- Design bias: work offline with optional asset downloads/setup.

## 2. Repository Map (What Matters Most)

- mobile/TarteelMobile: MAUI app, UI, app services, ASR integration, local data repository.
- src/LocalRecitationCore: framework-agnostic recitation contracts and orchestration.
- tests/TarteelMobile.Tests: xUnit unit tests (currently focused on viewmodel/settings behavior).
- offline-assets: expected location for local models, data, and helper executables.
- scripts/fetch-full-quran.ps1: one-time dataset fetch utility.
- scripts/release/windows-package.ps1: Windows packaging automation.
- .github/workflows/windows-packaging-release.yml: CI validation + manual packaging workflow.
- docs/app-functionality.md: high-level functional writeup (partially outdated; see section 13).

## 3. Architecture Summary

Current runtime architecture is in-process and local:

1. UI page/viewmodel captures user intent to start/stop recitation.
2. Audio service records and emits chunks.
3. Recitation service forwards chunks to local core orchestrator.
4. Core orchestrator runs ASR, merges transcript context, invokes matcher.
5. Match result is emitted to UI and persisted as progress.
6. UI updates confidence/status/highlighting and optional diagnostics panels.

## 4. Dependency Injection and Startup

DI composition lives in mobile/TarteelMobile/MauiProgram.cs.

Important registrations:

- Mobile interfaces:
  - IAudioService -> AudioService
  - IRecitationService -> RecitationService
  - IVerseRepository -> LocalVerseRepository
  - ISessionService -> LocalSessionService
  - IOfflineReadinessService -> OfflineReadinessService
  - IAsrEngine -> LocalWhisperAsrEngine
- Core adapters:
  - Core IAsrEngine -> AsrEngineCoreAdapter
  - Core IVerseRepository -> VerseRepositoryCoreAdapter
  - Core IVerseMatcher -> PlaceholderVerseMatcher
  - Core IProgressStore -> DiagnosticsProgressStore
  - Core IRecitationOrchestrator -> OfflineRecitationOrchestrator

Config loading detail:

- appsettings.json is loaded from packaged assets via FileSystem.OpenAppPackageFileAsync.
- JSON is flattened manually into IConfiguration keys.
- Options are bound via custom reflection-based binder (not Microsoft options binder).

## 5. App Shell and UX Surface

Shell tabs (mobile/TarteelMobile/AppShell.xaml):

- Recite (RecitationPage)
- Progress (ProgressPage)

Recitation page includes:

- Surah/Ayah picker.
- Arabic verse panel and hide/show mode.
- Confidence progress bar.
- Advanced mode toggles for model status and ASR debug panel.
- Model missing/import UI.
- Mic start/stop and reset actions.

## 6. Recitation Runtime Flow

Main behavior:

- RecitationViewModel toggles recording.
- On start:
  - Clears session UI state.
  - Connects recitation pipeline.
  - Starts microphone recording.
  - Loads placeholder verse for immediate UI context.
- On incoming chunk:
  - Sends chunk to RecitationService.
- RecitationService:
  - Guards connection state and serializes chunk submission with SemaphoreSlim.
  - Calls core orchestrator SubmitAudioChunkAsync.
- OfflineRecitationOrchestrator:
  - Transcribes chunk with ASR.
  - Emits diagnostics for each chunk.
  - Merges transcript tokens with overlap logic.
  - Calls matcher.
  - Saves progress via IProgressStore.
  - Emits match events.
- ViewModel applies best-match progression logic and updates confidence/highlight/tajweed panels.

## 7. ASR Engine (LocalWhisperAsrEngine)

File: mobile/TarteelMobile/Services/Asr/LocalWhisperAsrEngine.cs

What it does:

- Uses Whisper.net in-process.
- Persists model files in app data directory.
- Supports primary/fallback tiers from config.
- Can download model if missing and URL is configured.
- Exposes download progress event for UI.
- Supports importing model from picker stream/file.
- Uses lifecycle locks to avoid race conditions across initialize/import/reload.

Important operational behavior:

- If model/factory setup fails and AllowMockWhenUnavailable is true, it can fall back to mock mode.
- In mock mode, transcription returns failure-like diagnostics instead of real transcript output.

## 8. Matching and Tajweed State

Current matcher is placeholder (src/LocalRecitationCore/Services/PlaceholderVerseMatcher.cs):

- Tokenizes and normalizes Arabic input.
- Uses heuristic token matching (lookahead, edit-distance similarity, prefix handling).
- Computes confidence from token match, character similarity, coverage, and overflow penalty.
- Uses TajweedRuleEngine for rule hints over mismatches.

This is useful scaffolding but is not a production-grade full-Quran aligner.

## 9. Local Data and Persistence

Primary repository: mobile/TarteelMobile/Services/VerseRepository.cs

Database:

- SQLite in LocalApplicationData/TarteelClone/data.
- Default file name from LocalQuranData.DatabaseFileName (quran-local.db).
- Tables created internally by repository:
  - verses
  - translations
  - memorization_progress
  - dataset_metadata

Bootstrap import order:

1. ExternalImportFile (if configured and exists).
2. Preferred packaged asset import file (default quran/import/full_quran.json).
3. Fallback seed asset import file (default quran/import/seed_verses.json).
4. Built-in hardcoded fallback verses (Surah 1).

Re-import behavior:

- If DB already has verses, repository compares asset count and metadata and can re-import when packaged dataset grows.

Progress persistence:

- DiagnosticsProgressStore calls IVerseRepository.RecordRecitationAsync.
- RecordRecitationAsync upserts using EMA-like smoothing:
  - new_score = old * 0.7 + incoming * 0.3

## 10. Readiness Checks

OfflineReadinessService startup checks include:

- Verse count > 0.
- Sample verse retrievable.
- Audio service availability state.
- Diagnostics log path configured.
- ASR initialize state and mock/real mode note.

The ASR check is marked non-required in readiness report; required checks focus on local app operability.

## 11. Build, Run, Test, Package

Typical commands (Windows):

- Install workload:
  - dotnet workload install maui
- Restore:
  - dotnet restore mobile/TarteelMobile/TarteelMobile.csproj -p:TargetFramework=net9.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
- Build app:
  - dotnet build mobile/TarteelMobile/TarteelMobile.csproj -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
- Run app:
  - dotnet run --project mobile/TarteelMobile/TarteelMobile.csproj -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
- Run tests:
  - dotnet test tests/TarteelMobile.Tests/TarteelMobile.Tests.csproj -f net9.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64

Packaging:

- Build-only validation:
  - powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1 -BuildOnly
- Publish/package:
  - powershell -ExecutionPolicy Bypass -File .\scripts\release\windows-package.ps1

CI workflow validates:

- MAUI workload install.
- Restore/build for core + app.
- packaging script dry-run build-only.

## 12. Test Coverage Reality

Current tests are light and mostly behavior/unit-level:

- RecitationPracticeSettings boundaries and panel visibility logic.
- ProgressViewModel summary formatting and empty state.

Gaps (high value future tests):

- LocalVerseRepository import/re-import behavior and schema migration safety.
- OfflineRecitationOrchestrator transcript merge/match sequencing.
- RecitationViewModel state transitions under start/stop/errors.
- LocalWhisperAsrEngine failure mode behavior and model import flow.

## 13. Source-of-Truth Rules and Known Drift

When docs conflict, trust code paths currently wired in MauiProgram.

Observed drift:

- docs/app-functionality.md says progress from live recitation may not persist.
- Current code in Services/Core/LocalCoreAdapters.cs persists matches through RecordRecitationAsync.

Actionable rule:

- Treat docs/app-functionality.md as conceptual overview.
- Validate implementation claims against current code before making changes.

## 14. Common Change Patterns

If you are adding production-quality matching:

1. Replace PlaceholderVerseMatcher with a real candidate search/alignment engine.
2. Keep IVerseMatcher contract stable if possible to reduce app impact.
3. Add deterministic tests in LocalRecitationCore for Arabic normalization and alignment scoring.
4. Verify RecitationViewModel confidence/status thresholds still make sense.

If you are improving progress analytics:

1. Extend memorization_progress schema cautiously.
2. Keep RecordRecitationAsync idempotent/upsert-based.
3. Add migration strategy before schema changes.
4. Keep ProgressViewModel user-key behavior aligned with LocalSessionService.

If you are hardening ASR:

1. Ensure model path resolution stays writable and package-safe.
2. Preserve lock ordering to avoid deadlocks.
3. Surface user-friendly errors in RecitationViewModel status and diagnostics logs.
4. Keep mock-mode fallback explicit in UI.

## 15. Operational Notes for AI Agents

- Prefer offline-safe changes unless a task explicitly introduces network dependency.
- Keep Windows target compatibility intact (net9.0-windows10.0.19041.0 with win-x64).
- Avoid breaking MauiProgram registration graph; a missing adapter registration can silently break runtime features.
- For behavioral claims, cite/inspect code in:
  - MauiProgram.cs
  - RecitationService.cs
  - OfflineRecitationOrchestrator.cs
  - LocalWhisperAsrEngine.cs
  - VerseRepository.cs
- If touching startup/config, verify appsettings asset reading still works when packaged.

## 16. Useful Paths

- README.md
- docs/app-functionality.md
- mobile/TarteelMobile/MauiProgram.cs
- mobile/TarteelMobile/App.xaml.cs
- mobile/TarteelMobile/AppShell.xaml
- mobile/TarteelMobile/ViewModels/RecitationViewModel.cs
- mobile/TarteelMobile/ViewModels/ProgressViewModel.cs
- mobile/TarteelMobile/Services/RecitationService.cs
- mobile/TarteelMobile/Services/VerseRepository.cs
- mobile/TarteelMobile/Services/Core/LocalCoreAdapters.cs
- mobile/TarteelMobile/Services/Asr/LocalWhisperAsrEngine.cs
- src/LocalRecitationCore/Services/OfflineRecitationOrchestrator.cs
- src/LocalRecitationCore/Services/PlaceholderVerseMatcher.cs
- scripts/fetch-full-quran.ps1
- scripts/release/windows-package.ps1
- .github/workflows/windows-packaging-release.yml

---

Last refreshed: 2026-06-08
