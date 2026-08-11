# Tarteel Clone — Implementation Status

**Date:** August 10, 2026  
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
- ✅ Needleman-Wunsch alignment, fuzzy similarity, LCS support, confidence scoring, and mismatch models exist in the current matcher.
- ✅ Surah context can constrain matching candidates.
- ⚠️ The main matcher is still named `PlaceholderVerseMatcher` and needs production-level full-Mushaf continuation tests before it is renamed or treated as authoritative.
- ❌ Robust continuation across ayah boundaries, assignment-constrained matching, omission/repetition handling, and broad accuracy validation remain incomplete.

### 2.4 Tajweed assessment

- ✅ Text-level rule detection covers Madd, Ghunna, Qalqalah, Idgham, Ikhfa, Iqlab, and Izhar.
- ✅ The Arabic rule tables and feedback labels are encoding-safe Unicode and are covered by seven direct regression tests.
- ✅ Phoneme-level analysis currently covers Madd, Ghunna, and Qalqalah using timestamps and audio features.
- ✅ Word timestamps now use real Whisper token timestamps (DTW timestamp from `SegmentData.Tokens`) instead of linear character-count interpolation.
- ✅ Word alignment exports `SpokenToExpectedPosition` from the matcher, linking every spoken word to its true ayah index.
- ✅ The phoneme analyzer correctly skips WAV headers on Windows, matching raw-PCM behavior.
- ✅ The text engine now checks all words for madd/ghunna/nun-sakinah traits, not just mismatched words.
- ✅ Combined violations are deduplicated by `(Position, Rule)`, preferring the audio-evidence (phoneme) version.
- ✅ 14 new accuracy tests cover timestamps, alignment, WAV handling, matched-word checks, phoneme thresholds, and dedup.
- ✅ Text and phoneme violations are combined in `OfflineRecitationOrchestrator` and surfaced in the recitation UI.
- ❌ Reliable phoneme-level Idgham, Ikhfa, Iqlab, Izhar, and Makhraj analysis is not complete.
- ❌ Tajweed violations are not yet stored as durable per-rule history.
- ⚠️ Feedback quality depends on Whisper transcription and word timestamps; heuristic detections must not be presented as authoritative teacher judgment.

### 2.5 Progress and review scheduling

- ✅ Per-verse EMA mastery score is persisted in SQLite.
- ✅ `ReviewScheduler` provides deterministic policies for new lessons, weak performance, due reviews, overdue reviews, priority, and bounded review intervals.
- ✅ `memorization_progress` now stores `next_review_at`, `attempt_count`, and `recent_error_count`.
- ✅ Existing local databases receive an additive schema upgrade for the new review fields.
- ✅ `GetDueProgressAsync` returns the due review queue using the same scheduler policy.
- ✅ The Progress page exposes a student-facing “Today’s review” section before practice history.
- ✅ The current verified test suite has 10 passing tests.
- ✅ Durable local learning plans, lesson assignments, recitation sessions, verse-level attempts, word mismatches, and tajweed violations are persisted in SQLite.
- ✅ Attempt writes are transactional, update EMA progress and `next_review_at` through `ReviewScheduler`, and recover across a new repository instance.
- ✅ Guided Today flow now generates bounded review/new-lesson assignments, starts assignment-aware recitation, persists completed attempts, and exposes retry/next actions.
- ⚠️ Assignment completion currently requires a perfect-confidence match before persistence; partial/error attempts remain visible in the active session but are not yet saved as durable attempts.

### 2.6 MAUI user interface

- ✅ Recitation page with microphone control, verse display, confidence feedback, transcription, mismatch highlighting, tajweed corrections, hidden-verse mode, and advanced diagnostics.
- ✅ Mushaf page with page navigation, Juz/Surah/Ayah navigation, 16-row layout, and verse highlighting.
- ✅ Progress page with Today’s review and practice history.
- ✅ Login page and local session scaffolding.
- ✅ Windows-only active build and publish configuration.
- ✅ Android implementation files are retained but excluded from active target frameworks.
- ✅ Progress now acts as the Today surface with a primary Start today action and per-assignment Start controls.
- ✅ Recitation accepts assignment handoff parameters and exposes Retry and Next actions while preserving manual recitation.
- ⚠️ The shell is still organized around Recite/Mushaf/Progress rather than a dedicated student dashboard.
- ❌ Curriculum pacing, streaks, and broader teacher-like recommendations are not complete.

## 3. Important empty or incomplete areas

- `src/QuranEngine` remains a scaffold for future line-level Mushaf layout work.
- `src/SearchService` remains a scaffold for future semantic search.
- `src/UserService` and `src/Api` remain scaffolds; the current app intentionally uses local session behavior.
- Reference recitation playback and word-level audio are not implemented.
- The current database has progress and review metadata but not the full lesson/session/attempt/error domain.

## 4. Current critical gaps

1. Durable lesson assignments, recitation sessions, attempts, mismatches, and tajweed errors.
2. Durable partial/error attempt history and richer guided completion states beyond perfect-match completion.
3. Full-Mushaf matcher continuation and accuracy validation.
4. Persistent per-rule tajweed history and actionable weak-area feedback.
5. Reliable phoneme-level coverage for the remaining tajweed rules and carefully scoped Makhraj analysis.
6. Reference recitation playback with a verified offline asset and licensing contract.
7. Verified line-level Indo-Pak/Madani Mushaf data and authentic 16-line rendering.
8. A student curriculum with daily targets, new lesson progression, review balance, streaks, and clear next steps.

## 5. Priority order

1. **Lesson and attempt domain:** add durable assignments, sessions, attempts, mismatches, and tajweed-error persistence.
2. **Guided Today workflow:** connect the due queue to assignment-aware recitation, completion, retry, and next-item actions.
3. **Full-Mushaf matching:** replace the placeholder name only after continuation and edge-case behavior is covered by tests.
4. **Tajweed history:** aggregate recurring rule errors and show student-facing corrective guidance.
5. **Expanded phoneme tajweed:** implement only rules supported by reliable audio features and validation data.
6. **Reference playback:** add offline reciter audio through a licensing-safe asset contract.
7. **Authentic 16-line Mushaf:** integrate verified line-level data, font, and page layout.
8. **Curriculum and teacher-like guidance:** daily targets, pacing, weak-area recommendations, streaks, and next actions.

## 6. Verified milestone and next priority

The lesson and attempt domain is now implemented and verified in SQLite:

- create and fetch a local learning plan;
- create idempotent assignments for new lessons and reviews;
- open, close, and recover recitation sessions;
- persist one attempt per verse rather than one record per audio chunk;
- persist word mismatches and tajweed violations with cascade-safe child tables;
- recompute EMA progress and next review from the attempt result;
- verify restart recovery and error persistence through repository tests.

The guided Today workflow is implemented and verified: bounded assignments are generated from the learning plan, Progress starts an assignment-aware recitation, completed attempts persist transactionally, and Recitation exposes retry and next-item actions. The next priority is durable partial/error attempt history and richer completion states.

## 7. Verification evidence

The latest verified checks are:

- `dotnet build tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — succeeded with 0 warnings and 0 errors.
- `dotnet test tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~TajweedAccuracyTests` — 14 passed, 0 failed.
- `dotnet test tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~TodayWorkflowServiceTests` — 1 passed, 0 failed.
- `dotnet test tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore --filter FullyQualifiedName~LearningDomainRepositoryTests` — 2 passed, 0 failed.
- `dotnet test tests/TarteelMobile/TarteelMobile.Tests.csproj --framework net9.0-windows10.0.19041.0 --runtime win-x64 --no-restore` — 34 passed, 0 failed.
- `tests/TarteelMobile.Tests/TajweedAccuracyTests.cs` covers token timestamps, spoken-to-expected alignment, WAV header handling, matched-word text checks, phoneme analysis, and dedup.
- `tests/TarteelMobile.Tests/TodayWorkflowServiceTests.cs` verifies review-before-new ordering and idempotent assignment generation.
- `tests/TarteelMobile.Tests/LearningDomainRepositoryTests.cs` verifies plan/assignment/session lifecycle, one verse-level attempt, mismatch and tajweed persistence, progress recomputation, and restart recovery.
- Packaged Quran asset validation remains 6,236 verses, 114 surahs, and 604 Mushaf pages.
- Android remains absent from `mobile/TarteelMobile/TarteelMobile.csproj` active `TargetFrameworks`.
- The guided Today workflow is implemented and verified; the next milestone is durable partial/error attempt history.
