using TarteelMobile.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class CurriculumServiceTests
{
    [Fact]
    public void Juz30Path_CoversSurahs78To114InOrder()
    {
        var service = new CurriculumService();
        var path = service.GetLearningPath(CurriculumPath.Juz30);

        Assert.Equal(564, path.Count); // surahs 78–114 total ayahs
        Assert.Equal((78, 1), path[0]);
        Assert.Equal((78, 40), path[39]); // surah 78 has 40 ayahs
        Assert.Equal((114, 6), path[^1]);
        Assert.Equal(114, path[^1].SurahNum);
        Assert.Equal(path.Count, path.Distinct().Count());
    }

    [Fact]
    public void SequentialPath_CoversEveryVerseExactlyOnce()
    {
        var service = new CurriculumService();
        var path = service.GetLearningPath(CurriculumPath.Sequential);

        Assert.Equal(6236, path.Count);
        Assert.Equal(6236, path.Distinct().Count());
        Assert.Equal((1, 1), path[0]);
        Assert.Equal((114, 6), path[^1]);
    }

    [Fact]
    public void ShortSurahsPath_StartsWithFatihaThenShortestFirst()
    {
        var service = new CurriculumService();
        var path = service.GetLearningPath(CurriculumPath.ShortSurahs);

        Assert.Equal((1, 1), path[0]);
        Assert.Equal((1, 7), path[6]);
        // After Fatiha comes surah 114 (shortest), then 113, etc.
        Assert.Equal((114, 1), path[7]);
        Assert.Equal(571, path.Count); // Fatiha (7) + Juz 30 (564)
        Assert.Equal(path.Count, path.Distinct().Count());
    }

    [Fact]
    public void AllPaths_HaveNoDuplicates()
    {
        var service = new CurriculumService();
        foreach (var path in new[] { CurriculumPath.Juz30, CurriculumPath.ShortSurahs, CurriculumPath.Sequential })
        {
            var verses = service.GetLearningPath(path);
            Assert.Equal(verses.Count, verses.Distinct().Count());
            Assert.All(verses, v => Assert.InRange(v.SurahNum, 1, 114));
        }
    }
}
