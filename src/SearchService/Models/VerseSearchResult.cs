namespace TarteelClone.SearchService.Models;

public record VerseSearchResult(
    int    SurahNum,
    int    AyahNum,
    string ArabicText,
    string Translation,
    double Score);
