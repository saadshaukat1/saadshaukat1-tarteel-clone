# App functionality

This document describes what **this repository’s Tarteel-style clone** actually does today, and how that relates to the well-known **[Tarteel](https://tarteel.ai/)** Quran companion (AI recitation tracking, memorization support, etc.).

---

## How the real Tarteel app is meant to behave (reference product)

[Tarteel](https://apps.apple.com/app/id1391009396) is marketed as an AI-assisted Quran memorization companion. Typical expectations include:

| Area | Typical behavior |
|------|-------------------|
| **Recitation follow-along** | User recites into the microphone; the app listens and tries to recognize Arabic speech and **scroll or highlight verses** that match what was spoken. |
| **Voice-driven navigation** | “Voice search”: reciting a snippet to **jump** to the matching verse (often described informally like “Shazam for Quran”). |
| **Memorization aids** | Optional modes (often premium) such as **mistake highlighting**, comparisons to a reference reading, revision goals, streaks/analytics. |
| **Listening** | Playback from various reciters for listening practice. |
| **Content** | Full Quran text, translations, sometimes script/layout choices (e.g. Madani vs Indo-Pak). |

This project is **not** a drop-in replica of that commercial stack; it is an **offline-first Windows desktop prototype** implementing a **subset** of the pipeline (local microphone → local speech recognition → local text store).

---

## What this codebase implements today

### Product shape

- **Platform:** [.NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/) app targeting **Windows** (`mobile/TarteelMobile`).
- **Mode:** **Offline-first** — designed to run with **local Whisper** (`whisper-cli`) and **local SQLite** Quran data, without depending on backend services during recitation (`README.md`).

### User-facing surfaces

The shell exposes two tabs (`AppShell.xaml`):

1. **Recite** (`RecitationPage`)
   - Large **Arabic** display (right-aligned), **confidence** progress bar, and **status text** (“Listening…”, match quality, Surah:Aya hints when thresholds are met).
   - **Microphone toggle:** starts recording and connects the **in-process** recitation pipeline; stop pauses recording and disconnects (`RecitationViewModel`).
   - **Reset session:** stops audio, disconnects pipeline, clears on-screen verse/confidence/mismatches, and logs out the **local session** if one was authenticated (`StopSessionAsync`).

2. **Progress** (`ProgressPage`)
   - Lists verses from the local **`memorization_progress`** table for the active user key (`ProgressViewModel` + `IVerseRepository.GetMemorizedVersesAsync`).
   - Shows “No verses recorded yet.” when the table has no rows for that user (`ProgressPage.xaml`).
   - **Note:** Match handling in the pipeline currently uses `DiagnosticsProgressStore`, which **logs** matches but does **not** call `RecordRecitationAsync` on `LocalVerseRepository`. So unless data is populated by future code or manually, **this list may stay empty during normal recitation.**

### Offline setup and diagnostics

On startup, **`OfflineReadinessService`** verifies:

| Check | Meaning |
|-------|---------|
| Local SQLite Quran store | Database has at least one verse row. |
| Local verse repository | A sample verse (e.g. 1:1) can be loaded. |
| Audio service | Service is reachable (reports idle/recording state). |
| Diagnostics sink | A log file path is configured (`IAppDiagnosticsService`). |
| Local ASR | Whisper path/model resolved and engine **not** in mock mode (`LocalWhisperAsrEngine`). |

If readiness fails, the user sees **“Offline setup incomplete”** with a summary (`App.xaml.cs`).

Configuration comes from packaged **`appsettings.json`** (e.g. DB file name, import asset paths, Whisper runtime/model tiers). See `Resources/Raw/appsettings.json` and `README.md` for recommended `offline-assets/` layout.

### Local Quran data

- **`LocalVerseRepository`** initializes SQLite under the user’s local app data (`TarteelClone/data`), applies schema for **verses**, **translations**, and **memorization_progress**.
- Bootstrap import order: optional **external file** (`ExternalImportFile`) → packaged **`quran/import/full_quran.json`** → **`seed_verses.json`** → hardcoded fallback verses for Surah 1 (`VerseRepository.cs`).
- Default translation preference in config is **English / Saheeh International** (`LocalQuranData` section).

### Recitation pipeline (technical)

Flow:

1. **Audio:** `IAudioService` captures microphone chunks (platform-specific on Windows).
2. **Orchestration:** `OfflineRecitationOrchestrator` (`src/LocalRecitationCore/`):
   - Initializes ASR, accepts chunks, transcribes each chunk, invokes verse matching, notifies progress store, raises **match events**.
3. **ASR:** `LocalWhisperAsrEngine` runs **local Whisper** when `whisper-cli` and models are installed; otherwise it can fall back to **mock behavior** depending on `AllowMockWhenUnavailable`.
4. **Matching:** **`PlaceholderVerseMatcher`** loads verse **1:1** from the core verse repository and returns a simplistic result (confidence heuristics, **no real alignment** across the mushaf). **This is explicitly a placeholder** until a full matcher is integrated (`PlaceholderVerseMatcher.cs`).

The MAUI **`RecitationService`** adapts core events into UI **`MatchResult`** models (confidence, mismatches collection, Arabic text).

### Session and login

- **`LocalSessionService`** provides a **local-only** “login/register” validation (email format + minimum password length). It is **not** a remote OAuth/API flow.
- **`RecitationService.RequiresAuthentication`** is **`false`** for offline desktop, so **recitation does not require login** (`RecitationService.cs`).
- **`LoginPage`** exists in DI but **is not** part of the main `TabBar` shell — it is scaffolding for scenarios where authentication might gate features later (`MauiProgram.cs`, `AppShell.xaml`).

### Backend / API notes

Historical or complementary services may exist under `src/` (e.g. Python ASR service), but the **documented desktop path** is **local orchestration**. Do not assume cloud APIs unless you explicitly wire them; the README emphasizes **offline** operation.

---

## Mapping: Tarteel-style goals vs current clone

| Concept (Tarteel-like) | In this repo now |
|------------------------|-------------------|
| Mic → real-time Quran tracking | Partially — ASR runs locally; **verse detection is placeholder** (not full-mushaf follow-along). |
| Confidence / mistakes UI | Scaffolded (`MatchResult`, thresholds in `RecitationViewModel`) but matcher output is **not** production-grade. |
| Progress / memorization UX | SQLite schema and **Progress** screen exist; **automatic progress updates from live recitation** are **not wired** through `RecordRecitationAsync` yet. |
| Voice search / jump-to-ayah | Not implemented as a dedicated feature. |
| Multi-reciter playback | Out of scope in the described MAUI flow. |

---

## Summary

**Expected product behavior:** A **Windows-first**, **offline-capable** “recitation lab” — capture speech, transcribe with **Whisper**, hold Quran text locally in **SQLite**, and surface **match/confidence UI** toward a fuller Tarteel-like experience.

**Current reality:** Infrastructure is substantial (audio, Whisper adapter, SQLite import, readiness checks, orchestration). **Functional parity with consumer Tarteel** is limited by **`PlaceholderVerseMatcher`** and the **lack of persistent progress updates** from the live pipeline; those are natural next layers for contributors.

---

## Related files

| Topic | Location |
|-------|-----------|
| High-level architecture | `README.md` |
| Tabs / navigation | `mobile/TarteelMobile/AppShell.xaml` |
| Recitation UI logic | `mobile/TarteelMobile/ViewModels/RecitationViewModel.cs` |
| Pipeline orchestration | `src/LocalRecitationCore/Services/OfflineRecitationOrchestrator.cs` |
| Local Whisper | `mobile/TarteelMobile/Services/Asr/LocalWhisperAsrEngine.cs` |
| Quran DB + import | `mobile/TarteelMobile/Services/VerseRepository.cs` |
| Matcher placeholder | `src/LocalRecitationCore/Services/PlaceholderVerseMatcher.cs` |
