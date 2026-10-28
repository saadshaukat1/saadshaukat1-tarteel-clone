using System.Text.RegularExpressions;

namespace TarteelMobile.Services;

public static class ArabicTextNormalizer
{
    // Tashkeel / Harakat (Fatha, Damma, Kasra, Sukun, Tanween, Shadda, Maddah)
    private static readonly Regex HarakatRegex = new(@"[\u064B-\u065F\u0653\u0654\u0655]", RegexOptions.Compiled);

    // Quranic recitation marks (Waqf stop signs, rub el hizb, sajdah, etc.)
    private static readonly Regex QuranicAnnotationsRegex = new(@"[\u06D6-\u06ED\u06E9\u06EA\u06DE\u06DF\u06E0\u06E1\u06E2\u06E3\u06E4\u06E5\u06E6\u06E7\u06E8\u0600-\u0605]", RegexOptions.Compiled);

    // Alef variations
    private static readonly Regex AlefRegex = new(@"[إأآٱ]", RegexOptions.Compiled);

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 1. Convert Uthmani Quranic spelling patterns where dagger alif represents a full alif:
        // 'وٰ' (waw + dagger alif, e.g. الصلوة / الحيوة) -> 'ا' (الصلاة / الحياة)
        var normalized = input.Replace("وٰ", "ا")
                              .Replace("وَٰ", "ا");

        // Convert dagger alif (U+0670) to standard alif 'ا' (e.g. تَبَـٰرَكَ -> تبارك, سَمَـٰوَٰتٍ -> سماوات)
        normalized = normalized.Replace("ـٰ", "ا")
                               .Replace("ٰ", "ا");

        // 2. Strip standard tashkeel and harakat (diacritics)
        normalized = HarakatRegex.Replace(normalized, string.Empty);

        // 3. Strip Quranic annotations & stop marks
        normalized = QuranicAnnotationsRegex.Replace(normalized, string.Empty);

        // 4. Remove tatweel (kashida)
        normalized = normalized.Replace("ـ", string.Empty);

        // 5. Normalize Alef forms
        normalized = AlefRegex.Replace(normalized, "ا");

        // 6. Normalize Taa Marbuta
        normalized = normalized.Replace("ة", "ه");

        // 7. Normalize Alef Maqsura and Hamza forms
        normalized = normalized.Replace("ى", "ي")
                               .Replace("ؤ", "و")
                               .Replace("ئ", "ي");

        // 8. Collapse double alifs if produced by transformation (e.g. 'اا' -> 'ا')
        normalized = Regex.Replace(normalized, @"ا+", "ا");

        // 9. Strip any remaining non-Arabic letters/digits/spaces
        normalized = Regex.Replace(normalized, @"[^\u0621-\u064A\s0-9]", string.Empty);

        // 10. Collapse multiple consecutive whitespaces
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized.Trim();
    }

    public static string[] TokenizeAndNormalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var rawWords = input.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = new List<string>(rawWords.Length);

        foreach (var word in rawWords)
        {
            var norm = Normalize(word);
            if (!string.IsNullOrWhiteSpace(norm))
            {
                tokens.Add(norm);
            }
        }

        return tokens.ToArray();
    }
}
