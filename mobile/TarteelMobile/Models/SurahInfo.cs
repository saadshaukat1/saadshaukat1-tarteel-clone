namespace TarteelMobile.Models;

public sealed record SurahInfo(
    int SurahNum,
    string NameArabic,
    string NameEnglish,
    string NameTransliteration,
    string RevelationType,
    int AyahCount);
