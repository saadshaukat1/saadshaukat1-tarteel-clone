using TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services;

public interface ICurriculumService
{
    IReadOnlyList<(int SurahNum, int AyahNum)> GetLearningPath(CurriculumPath path = CurriculumPath.Juz30);
}

public enum CurriculumPath
{
    Juz30 = 0,
    ShortSurahs = 1,
    Sequential = 2
}

public sealed class CurriculumService : ICurriculumService
{
    public IReadOnlyList<(int SurahNum, int AyahNum)> GetLearningPath(CurriculumPath path = CurriculumPath.Juz30)
    {
        return path switch
        {
            CurriculumPath.Juz30 => GetJuz30Path(),
            CurriculumPath.ShortSurahs => GetShortSurahsPath(),
            CurriculumPath.Sequential => GetSequentialPath(),
            _ => GetJuz30Path()
        };
    }

    /// <summary>
    /// Juz 30 (Juz 'Amma): Surahs 78–114, progressing from longer to shorter.
    /// This is the traditional memorization starting point.
    /// </summary>
    private static IReadOnlyList<(int, int)> GetJuz30Path()
    {
        return AllVersesInRange(78, 1, 114, 6);
    }

    /// <summary>
    /// Short surahs first: Al-Fatiha, then Juz 30 in reverse (shortest first).
    /// Best for absolute beginners.
    /// </summary>
    private static IReadOnlyList<(int, int)> GetShortSurahsPath()
    {
        var path = new List<(int, int)>();
        path.AddRange(AllVersesInRange(1, 1, 1, 7));

        for (var surah = 114; surah >= 78; surah--)
        {
            var ayahCount = GetAyahCount(surah);
            for (var ayah = 1; ayah <= ayahCount; ayah++)
            {
                path.Add((surah, ayah));
            }
        }

        return path;
    }

    /// <summary>
    /// Standard sequential order: 1:1 through end of Quran.
    /// </summary>
    private static IReadOnlyList<(int, int)> GetSequentialPath()
    {
        return AllVersesInRange(1, 1, 114, 6);
    }

    private static IReadOnlyList<(int, int)> AllVersesInRange(int startSurah, int startAyah, int endSurah, int endAyah)
    {
        var result = new List<(int, int)>();
        for (var surah = startSurah; surah <= endSurah; surah++)
        {
            var start = surah == startSurah ? startAyah : 1;
            var end = surah == endSurah ? endAyah : GetAyahCount(surah);
            for (var ayah = start; ayah <= end; ayah++)
            {
                result.Add((surah, ayah));
            }
        }
        return result;
    }

    private static int GetAyahCount(int surahNum) => surahNum switch
    {
        1 => 7,   2 => 286,  3 => 200,  4 => 176,  5 => 120,
        6 => 165,  7 => 206,  8 => 75,   9 => 129,  10 => 109,
        11 => 123,  12 => 111,  13 => 43,  14 => 52,  15 => 99,
        16 => 128,  17 => 111,  18 => 110,  19 => 98,  20 => 135,
        21 => 112,  22 => 78,   23 => 118,  24 => 64,  25 => 77,
        26 => 227,  27 => 93,   28 => 88,   29 => 69,  30 => 60,
        31 => 34,   32 => 30,   33 => 73,   34 => 54,  35 => 45,
        36 => 83,   37 => 182,  38 => 88,   39 => 75,  40 => 85,
        41 => 54,   42 => 53,   43 => 89,   44 => 59,  45 => 37,
        46 => 35,   47 => 38,   48 => 29,   49 => 18,  50 => 45,
        51 => 60,   52 => 49,   53 => 62,   54 => 55,  55 => 78,
        56 => 96,   57 => 29,   58 => 22,   59 => 24,  60 => 13,
        61 => 14,   62 => 11,   63 => 11,   64 => 18,  65 => 12,
        66 => 12,   67 => 30,   68 => 52,   69 => 52,  70 => 44,
        71 => 28,   72 => 28,   73 => 20,   74 => 56,  75 => 40,
        76 => 31,   77 => 50,   78 => 40,   79 => 46,  80 => 42,
        81 => 29,   82 => 19,   83 => 36,   84 => 25,  85 => 22,
        86 => 17,   87 => 19,   88 => 26,   89 => 30,  90 => 20,
        91 => 15,   92 => 21,   93 => 11,   94 => 8,   95 => 8,
        96 => 19,   97 => 5,    98 => 8,    99 => 8,   100 => 11,
        101 => 11,  102 => 8,   103 => 3,   104 => 9,  105 => 5,
        106 => 4,   107 => 7,   108 => 3,   109 => 6,  110 => 3,
        111 => 5,   112 => 4,   113 => 5,   114 => 6,
        _ => 1
    };
}
