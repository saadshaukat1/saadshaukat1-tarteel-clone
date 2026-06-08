# Plan: Juz Index, Surah Metadata & Full Quran Verse Matching

## Design Decisions

| Decision | Choice |
|----------|--------|
| Default search mode | Full Quran with word-to-verse inverted index |
| Performance | Inverted index: query `word_index` table for candidate verses (~20-200 instead of 6,236) |
| Seed data scope | All 30 juz + all 114 surahs |
| Juz picker UX | No manual scope picker — auto-match against full Quran, display juz/surah of result |
| Scope change mid-recording | Require stop/restart |

## Implementation Steps

### Step 1: Extract Arabic Normalizer Utility
File: `src/LocalRecitationCore/Utilities/ArabicNormalizer.cs` (NEW)
- Extract `NormalizeForComparison` and tokenize logic from `PlaceholderVerseMatcher`
- Must be identical normalization used by both matcher and word index builder

### Step 2: New Models
- `mobile/TarteelMobile/Models/JuzInfo.cs` (NEW)
- `mobile/TarteelMobile/Models/SurahInfo.cs` (NEW)

### Step 3: Database Schema Extension
File: `mobile/TarteelMobile/Services/VerseRepository.cs`
- Add `juz` table (30 rows — all juz boundaries)
- Add `surahs` table (114 rows — name_arabic, name_english, name_transliteration, revelation_type, ayah_count)
- Add `word_index` table (word, surah_num, ayah_num, position — inverted index)

### Step 4: Seed Data
File: `mobile/TarteelMobile/Services/VerseRepository.cs`
- `BuiltInJuzData` — all 30 juz boundaries
- `BuiltInSurahData` — all 114 surahs with metadata
- Seed on first launch if tables empty

### Step 5: Build Word Index
File: `mobile/TarteelMobile/Services/VerseRepository.cs`
- `BuildWordIndexAsync` — normalize each verse, tokenize, insert into word_index
- Runs once after verse import completes

### Step 6: New Repository Query Methods
File: `mobile/TarteelMobile/Services/VerseRepository.cs`
- `GetAllVersesAsync()` — all 6,236 verses
- `GetVersesByWordsAsync(words)` — IN query on word_index → DISTINCT surah/ayah → fetch verses
- `GetJuzForVerseAsync(surah, ayah)` — lookup which juz a verse belongs to
- `GetSurahAsync(surah)` — surah metadata
- `GetAllJuzAsync()`, `GetAllSurahsAsync()`, `GetSurahsByJuzAsync()`

### Step 7: Core Interface Extension
File: `src/LocalRecitationCore/Abstractions/RecitationContracts.cs`
- Extend `IVerseRepository` with `GetAllVersesAsync` and `GetCandidateVersesAsync`
- Add `JuzNum`, `SurahNameEnglish`, `SurahNameArabic` to `RecitationMatchResult`

### Step 8: Inverted-Index Matcher
File: `src/LocalRecitationCore/Services/PlaceholderVerseMatcher.cs`
- Use `ArabicNormalizer` utility
- Call `GetCandidateVersesAsync(contentWords)` for candidate pool
- Fallback to `GetAllVersesAsync()` if < 10 candidates

### Step 9: Adapter Updates
File: `mobile/TarteelMobile/Services/Core/LocalCoreAdapters.cs`
- Implement new core `IVerseRepository` methods

### Step 10: Juz/Surah Metadata on Match Results
File: `mobile/TarteelMobile/Services/RecitationService.cs`
- Populate `JuzNum`, `SurahNameEnglish`, `SurahNameArabic` when mapping match results
- Uses `GetJuzForVerseAsync` and `GetSurahAsync`

### Step 11: ViewModel + UI
- `RecitationViewModel.cs` — display-only properties for matched juz/surah
- `RecitationPage.xaml` — display row showing "Juz 30 · النبأ · An-Naba"
- `MatchResult.cs` — add JuzNum, SurahNameEnglish, SurahNameArabic fields

### Step 12: Tests
- Update `FakeVerseRepository` with new methods
- Test inverted index fallback behavior
