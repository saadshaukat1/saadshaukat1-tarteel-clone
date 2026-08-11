# Tarteel Clone — Implementation Status

**Date:** August 12, 2026  
**Project:** Offline-first Quran memorization and tajweed coaching app for a student in a maktab setting

## 1. Product objective

Build a Windows-first, offline-capable maktab qari replacement that helps a student:

- learn new Quran passages through a guided hifz curriculum;
- recite from memory and receive immediate verse and word-level feedback;
- identify and correct tajweed mistakes;
- follow a daily review schedule based on actual performance;
- track mastery, weak areas, streaks, and history;
- use a Mushaf view and reference recitation when needed;
- receive a clear next action instead of an unstructured list of verses.

Android implementation remains in the repository but is excluded from active build and publish targets until the Windows workflow is complete.

## 2. Verified implementation

### 2.1 Quran content and navigation

- ✅ `offline-assets/data/quran/import/full_quran.json` contains 6,236 verses.
- ✅ `mobile/TarteelMobile/Resources/Raw/quran/mushaf/page_map.json` contains 604 pages.
- ✅ Reference metadata contains 114 surahs and 30 juz.
- ✅ Local SQLite import supports packaged assets, external import files, seed fallback data, and dataset expansion on an existing installation.
- ✅ Verse lookup, translation lookup, surah/juz navigation, word index creation, and page lookup are implemented.
- ✅ Mushaf page navigation, ayah highlighting, and automatic next-page scrolling are implemented.
- ⚠️ Mushaf rendering still distributes verse text algorithmically across 16 rows rather than using verified line-level Indo-Pak/Madani data.
- ⚠️ Authentic Indo-Pak/Persian-script line-level content and font selection remain outstanding.

### 2.2 Audio and speech recognition

- ✅ Local Whisper.net integration is present.
- ✅ Tiny, base, small, and medium model tiers are supported.
- ✅ Model import/download and readiness diagnostics are implemented.
- ✅ Surah prompt bias, greedy/beam-search configuration, warmup, Arabic normalization, silence handling, RMS normalization, and overlapped PCM chunks are implemented.
- ✅ Windows audio capture is active.
- ⏸️ Android audio code remains in the repository but Android is excluded from active targets.

### 2.3 Verse matching

- ✅ Arabic normalization and word indexing are implemented.
- ✅ Needleman-Wunsch alignment, fuzzy similarity, LCS support, confidence scoring, and mismatch models exist in `LocalVerseMatcher` (renamed from `PlaceholderVerseMatcher`).
- ✅ Surah context can constrain matching candidates.
- ✅ `MarkCurrentAyahComplete()` signals ayah-boundary continuation — boosts the next verse's matching priority after a perfected recitation.
- ✅ `IRecitationService` and `IRecitationOrchestrator` both expose `MarkCurrentAyahComplete()` through the full pipeline.
- ⚠️ Robust continuation across ayah boundaries, assignment-constrained matching, omission/repetition handling, and broad accuracy validation remain incomplete.

### 2.4 Tajweed assessment

- ✅ Text-level rule detection covers Madd, Ghunna, Qalqalah, Idgham, Ikhfa, Iqlab, and Izhar.
- ✅ The Arabic rule tables and feedback labels are encoding-safe Unicode and are covered by seven direct regression tests.
- ✅ Phoneme-level analysis covers Madd, Ghunna, Qalqalah, Idgham, Ikhfa, and Makharij (articulation points).
- ✅ Word timestamps now use real Whisper token timestamps (DTW timestamp from `SegmentData.Tokens`) instead of linear character-count interpolation.
- ✅ Word alignment exports `SpokenToExpectedPosition` from the matcher, linking every spoken word to its true ayah index.
- ✅ The phoneme analyzer correctly skips WAV headers on Windows, matching raw-PCM behavior.
- ✅ The text engine now checks all words for madd/ghunna/nun-sakinah traits, not just mismatched words.
- ✅ Combined violations are deduplicated by `(Position, Rule)`, preferring the audio-evidence (phoneme) version.
- ✅ 14 accuracy tests cover timestamps, alignment, WAV handling, matched-word checks, phoneme thresholds, and dedup.
- ✅ Text and phoneme violations are combined in `OfflineRecitationOrchestrator` and surfaced in the recitation UI.
- ✅ **NEW: Idgham phoneme analysis** — detects nun-sakinah merging into ی, ر, م, ل, و, ن with/without nasalization using spectral centroid.
- ✅ **NEW: Ikhfa phoneme analysis** — detects concealed nun-sakinah before ت, ث, ج, د, ذ, ز, س, ش, ص, ض, ط, ظ, ف, ق, ك with nasal resonance.
- ✅ **NEW: Makhraj phoneme analysis** — detects common articulation-point confusions (ح↔خ, س↔ص, ذ↔ز, ظ↔ض, ق↔ك, ط↔ت) using spectral centroid thresholds.
- ❌ Iqlab and Izhar phoneme analysis is not complete.
- ⚠️ Feedback quality depends on Whisper transcription and word timestamps; heuristic detections must not be presented as authoritative teacher judgment.

### 2.5 Progress and review scheduling

- ✅ Per-verse EMA mastery score is persisted in SQLite.
- ✅ `ReviewScheduler` provides deterministic policies for new lessons, weak performance, due reviews, overdue reviews, priority, and bounded review intervals.
- ✅ `memorization_progress` now stores `next_review_at`, `attempt_count`, and `recent_error_count`.
- ✅ Existing local databases receive an additive schema upgrade for the new review fields.
- ✅ `GetDueProgressAsync` returns the due review queue using the same scheduler policy.
- ✅ The Progress page exposes a student-facing "Today's review" section before practice history.
- ✅ The current verified test suite has 34 passing tests.
- ✅ Durable local learning plans, lesson assignments, recitation sessions, verse-level attempts, word mismatches, and tajweed violations are persisted in SQLite.
- ✅ Attempt writes are transactional, update EMA progress and `next_review_at` through `ReviewScheduler`, and recover across a new repository instance.
- ✅ Guided Today flow now generates bounded review/new-lesson assignments, starts assignment-aware recitation, persists completed attempts, and exposes retry/next actions.
- ✅ **NEW: Durable partial/error attempt history** — attempts above 0.65 confidence are saved as partial (assignment stays InProgress); attempts above 0.90 confidence with 0 mismatches are saved as perfect (assignment marked Completed). Partial attempts are auto-saved on mic stop and session reset.
- ✅ **NEW: Per-rule tajweed error history** — `tajweed_error_history` table tracks cumulative error counts per (user, surah, ayah, rule). `GetTajweedRuleSummariesAsync()` returns aggregated per-rule stats. The Progress page loads and exposes tajweed summaries.

### 2.6 MAUI user interface

- ✅ Recitation page with microphone control, verse display, confidence feedback, transcription, mismatch highlighting, tajweed corrections, hidden-verse mode, and advanced diagnostics.
- ✅ Mushaf page with page navigation, Juz/Surah/Ayah navigation, 16-row layout, and verse highlighting.
- ✅ Progress page with Today's review, practice history, and tajweed error summaries.
- ✅ Login page and local session scaffolding.
- ✅ Windows-only active build and publish configuration.
- ✅ Android implementation files are retained but excluded from active target frameworks.
- ✅ Progress now acts as the Today surface with a primary Start today action and per-assignment Start controls.
- ✅ Recitation accepts assignment handoff parameters and exposes Retry and Next actions while preserving manual recitation.
- ⚠️ The shell is still organized around Recite/Mushaf/Progress rather than a dedicated student dashboard.
- ❌ Curriculum pacing, streaks, and broader teacher-like recommendations are not complete.

### 2.7 Reference recitation playback

- ✅ `AudioPlaybackService` provides `IAudioPlaybackService` with verse-level and range playback.
- ✅ `HasReferenceAudio(surah, ayah)` checks for local audio file availability.
- ✅ Platform shell-execute playback via Windows default media player.
- ⚠️ No qari audio assets are bundled — licensing and curation of offline reciter files is required.

## 3. Important empty or incomplete areas

- `src/QuranEngine` remains a scaffold for future line-level Mushaf layout work.
- `src/SearchService` remains a scaffold for future semantic search.
- `src/UserService` and `src/Api` remain scaffolds; the current app intentionally uses local session behavior.
- The current database has progress, review metadata, and the full lesson/session/attempt/error domain.

## 4. Current gaps (all remaining)

1. **Authentic 16-line Mushaf** — verified line-level Indo-Pak/Madani data, font, and page layout.
2. **Student curriculum** — daily targets, streaks, pacing, teacher-like guidance, and dedicated dashboard shell navigation.
3. **Reference audio assets** — license, select, and bundle offline qari recitation files for playback.
4. **Iqlab/Izhar phoneme analysis** — extend `TajweedPhonemeAnalyzer` for remaining rule coverage.
5. **Full-mushaf matcher continuation tests** — omission/repetition handling and broad accuracy validation at Mushaf scale.
6. **QuranEngine scaffold** — fill the empty 16-liner text layout engine project.
7. **SearchService scaffold** — fill the empty semantic search project.
8. **UserService + Api scaffolds** — fill the empty user management and API projects.

## 5. Priority order

1. **Authentic 16-line Mushaf** — integrate verified line-level data, font, and page layout.
2. **Student curriculum and dashboard** — daily targets, pacing, streaks, weak-area recommendations, and a dedicated dashboard shell.
3. **Reference audio assets** — source licensed qari recordings and wire to `AudioPlaybackService`.
4. **Iqlab/Izhar phoneme** — complete remaining tajweed rule coverage.
5. **Mushaf-scale matcher tests** — continuation, omission, repetition, and accuracy at full 604-page / 6,236-verse scale.
6. **Fill empty scaffolds** — QuranEngine, SearchService, UserService, Api.

## 6. Verification evidence

The latest verified checks are:

- `dotnet build tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — succeeded with 0 warnings and 0 errors.
- `dotnet test tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — 34 passed, 0 failed.
- `TajweedAccuracyTests` (14) — token timestamps, spoken-to-expected alignment, WAV header handling, matched-word text checks, phoneme analysis, and dedup.
- `TajweedRuleEngineTests` (7) — text-level rule coverage for all 7 rules.
- `ReviewSchedulerTests` (10) — deterministic scheduling policies.
- `LearningDomainRepositoryTests` (2) — plan/assignment/session lifecycle, one verse-level attempt, mismatch and tajweed persistence, progress recomputation, restart recovery.
- `TodayWorkflowServiceTests` (1) — review-before-new ordering and idempotent assignment generation.
- `ProgressViewModelTests` — progress summary formatting and empty-state display.
- Packaged Quran asset validation remains 6,236 verses, 114 surahs, and 604 Mushaf pages.
- Android remains absent from `mobile/TarteelMobile/TarteelMobile.csproj` active `TargetFrameworks`.
