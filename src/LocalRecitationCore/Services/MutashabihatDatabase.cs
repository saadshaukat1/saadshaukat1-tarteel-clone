using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Curated database of famous Mutashabihat (verses with identical/similar phrasing across Surahs)
/// used by Madrasa Quran teachers to test students' mastery and distinguish true memorization.
/// </summary>
public static class MutashabihatDatabase
{
    private static readonly MutashabihaChallenge[] Challenges =
    [
        new(
            Id: 1,
            PromptVerseArabic: "وَإِذْ قُلْنَا ادْخُلُوا هَٰذِهِ الْقَرْيَةَ فَكُلُوا مِنْهَا حَيْثُ شِئْتُمْ رَغَدًا",
            SurahNum: 2,
            AyahNum: 58,
            SurahNameArabic: "سورة البقرة",
            SurahNameEnglish: "Al-Baqarah",
            TargetContinuationArabic: "وَادْخُلُوا الْبَابَ سُجَّدًا وَقُولُوا حِطَّةٌ نَّغْفِرْ لَكُمْ خَطَايَاكُمْ ۚ وَسَنَزِيدُ الْمُحْسِنِينَ",
            SimilarVerseSurahArabic: "سورة الأعراف (الآية ١٦١): ﴿وَإِذْ قِيلَ لَهُمُ اسْكُنُوا هَٰذِهِ الْقَرْيَةَ... وَقُولُوا حِطَّةٌ وَادْخُلُوا الْبَابَ سُجَّدًا نَّغْفِرْ لَكُمْ خَطِيئَاتِكُمْ...﴾",
            ExplanationArabic: "في البقرة: (ادْخُلُوا) مع (رَغَدًا) وتقديم (وَادْخُلُوا الْبَابَ) على (وَقُولُوا حِطَّةٌ) وبصيغة (نَّغْفِرْ لَكُمْ خَطَايَاكُمْ). وفي الأعراف: (اسْكُنُوا) بدون رغدا وبصيغة (نَّغْفِرْ لَكُمْ خَطِيئَاتِكُمْ).",
            ExplanationEnglish: "Al-Baqarah uses 'Idkhulu' with 'raghadan' and mentions entering the gate before saying Hittah. Al-A'raf uses 'Uskunu' and inverts the order."
        ),
        new(
            Id: 2,
            PromptVerseArabic: "وَاتَّقُوا يَوْمًا لَّا تَجْزِي نَفْسٌ عَن نَّفْسٍ شَيْئًا وَلَا يُقْبَلُ مِنْهَا شَفَاعَةٌ",
            SurahNum: 2,
            AyahNum: 48,
            SurahNameArabic: "سورة البقرة",
            SurahNameEnglish: "Al-Baqarah",
            TargetContinuationArabic: "وَلَا يُؤْخَذُ مِنْهَا عَدْلٌ وَلَا هُمْ يُنصَرُونَ",
            SimilarVerseSurahArabic: "سورة البقرة (الآية ١٢٣): ﴿وَاتَّقُوا يَوْمًا لَّا تَجْزِي نَفْسٌ عَن نَّفْسٍ شَيْئًا وَلَا يُقْبَلُ مِنْهَا عَدْلٌ وَلَا تَنفَعُهَا شَفَاعَةٌ وَلَا هُمْ يُنصَرُونَ﴾",
            ExplanationArabic: "في الآية ٤٨: قُدِّمت الشفاعة على العدل (وَلَا يُقْبَلُ مِنْهَا شَفَاعَةٌ وَلَا يُؤْخَذُ مِنْهَا عَدْلٌ). وفي الآية ١٢٣: قُدِّم العدل على الشفاعة (وَلَا يُقْبَلُ مِنْهَا عَدْلٌ وَلَا تَنفَعُهَا شَفَاعَةٌ).",
            ExplanationEnglish: "Ayah 48 places 'Shafa'ah' before 'Adl'. Ayah 123 inverts it, placing 'Adl' before 'Shafa'ah' and uses 'tanfa'uha' instead of 'yuqbalu'."
        ),
        new(
            Id: 3,
            PromptVerseArabic: "وَيَقُولُونَ مَتَىٰ هَٰذَا الْوَعْدُ إِن كُنتُمْ صَادِقِينَ",
            SurahNum: 67,
            AyahNum: 25,
            SurahNameArabic: "سورة الملك",
            SurahNameEnglish: "Al-Mulk",
            TargetContinuationArabic: "قُلْ إِنَّمَا الْعِلْمُ عِندَ اللَّهِ وَإِنَّمَا أَنَا نَذِيرٌ مُّبِينٌ",
            SimilarVerseSurahArabic: "وردت في يونس (٤٨)، الأنبياء (٣٨)، النمل (٧١)، سبأ (٢٩)، يس (٤٨)، والملك (٢٥).",
            ExplanationArabic: "جوابها في الملك: (قُلْ إِنَّمَا الْعِلْمُ عِندَ اللَّهِ وَإِنَّمَا أَنَا نَذِيرٌ مُّبِينٌ). وفي يس: (مَا يَنظُرُونَ إِلَّا صَيْحَةً وَاحِدَةً). وفي الأنبياء: (لَوْ يَعْلَمُ الَّذِينَ كَفَرُوا حِينَ لَا يَكُفُّونَ).",
            ExplanationEnglish: "Response in Al-Mulk: 'Say, the knowledge is only with Allah, and I am only a clear warner.' In Ya-Sin: 'They do not await except a single blast.'"
        ),
        new(
            Id: 4,
            PromptVerseArabic: "قُلْ سِيرُوا فِي الْأَرْضِ ثُمَّ انظُرُوا كَيْفَ كَانَ عَاقِبَةُ الْمُكَذِّبِينَ",
            SurahNum: 6,
            AyahNum: 11,
            SurahNameArabic: "سورة الأنعام",
            SurahNameEnglish: "Al-An'am",
            TargetContinuationArabic: "قُل لِّمَن مَّا فِي السَّمَاوَاتِ وَالْأَرْضِ ۖ قُل لِّلَّهِ",
            SimilarVerseSurahArabic: "سورة النحل (٣٦)، سورة النمل (٦٩)، سورة العنكبوت (٢٠)، وسورة الروم (٤٢).",
            ExplanationArabic: "في الأنعام جاءت بلفظ (ثُمَّ انظُرُوا كَيْفَ كَانَ عَاقِبَةُ الْمُكَذِّبِينَ). وفي غيرها جاءت بالفاء (فَانظُرُوا).",
            ExplanationEnglish: "Al-An'am uniquely uses 'thumma'nzhuru', whereas other Surahs use 'fa'nzhuru'."
        ),
        new(
            Id: 5,
            PromptVerseArabic: "هَٰذَا يَوْمُ لَا يَنطِقُونَ",
            SurahNum: 77,
            AyahNum: 35,
            SurahNameArabic: "سورة المرسلات",
            SurahNameEnglish: "Al-Mursalat",
            TargetContinuationArabic: "وَلَا يُؤْذَنُ لَهُمْ فَيَعْتَذِرُونَ ۝ وَيْلٌ يَوْمَئِذٍ لِّلْمُكَذِّبِينَ",
            SimilarVerseSurahArabic: "سورة النبأ وسورة التكوير.",
            ExplanationArabic: "تتبعها الآية ٣٦: (وَلَا يُؤْذَنُ لَهُمْ فَيَعْتَذِرُونَ) ثم فاصلة التكذيب.",
            ExplanationEnglish: "Followed by verse 36: 'Nor will it be permitted for them to offer excuses'."
        ),
        new(
            Id: 6,
            PromptVerseArabic: "تَبَارَكَ الَّذِي بِيَدِهِ الْمُلْكُ وَهُوَ عَلَىٰ كُلِّ شَيْءٍ قَدِيرٌ",
            SurahNum: 67,
            AyahNum: 1,
            SurahNameArabic: "سورة الملك",
            SurahNameEnglish: "Al-Mulk",
            TargetContinuationArabic: "الَّذِي خَلَقَ الْمَوْتَ وَالْحَيَاةَ لِيَبْلُوَكُمْ أَيُّكُمْ أَحْسَنُ عَمَلًا ۚ وَهُوَ الْعَزِيزُ الْغَفُورُ",
            SimilarVerseSurahArabic: "سورة الفرقان (الآية ١): ﴿تَبَارَكَ الَّذِي نَزَّلَ الْفُرْقَانَ عَلَىٰ عَبْدِهِ لِيَكُونَ لِلْعَالَمِينَ نَذِيرًا﴾",
            ExplanationArabic: "افتتحت سورة الملك والفرقان بلفظ (تَبَارَكَ الَّذِي)، والملك بالملك والقدرة، والفرقان بإنزال الكتاب والنذارة.",
            ExplanationEnglish: "Both Al-Furqan and Al-Mulk open with 'Tabaraka alladhi', one with sending the Criterion and the other with dominion."
        )
    ];

    public static IReadOnlyList<MutashabihaChallenge> GetAllChallenges() => Challenges;

    public static MutashabihaChallenge GetRandomChallenge()
    {
        var idx = Random.Shared.Next(Challenges.Length);
        return Challenges[idx];
    }

    public static MutashabihaChallenge? GetChallengeForSurah(int surahNum)
    {
        return Challenges.FirstOrDefault(c => c.SurahNum == surahNum);
    }
}
