using TarteelMobile.Models;
using TarteelMobile.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class BlindRecitationCanvasTests
{
    [Fact]
    public void ArabicTextNormalizer_StripsTashkeelAndDaggerAlif()
    {
        // "تَبَـٰرَكَ ٱلَّذِى" -> "تبارك الذي"
        var input = "تَبَـٰرَكَ ٱلَّذِى";
        var normalized = ArabicTextNormalizer.Normalize(input);

        Assert.Equal("تبارك الذي", normalized);
    }

    [Fact]
    public void ArabicTextNormalizer_StripsQuranicAnnotationsAndWaqfSigns()
    {
        // "عَمَلًا ۚ وَهُوَ" -> "عملا وهو"
        var input = "عَمَلًا ۚ وَهُوَ";
        var normalized = ArabicTextNormalizer.Normalize(input);

        Assert.Equal("عملا وهو", normalized);
    }

    [Fact]
    public void AlignmentEngine_ExactMatchAdvancesCursor()
    {
        var engine = new QuranAlignmentEngine();
        var (words, _) = MushafLayoutService.BuildPage562Layout(380f, 560f);

        // Word 0 is "تَبَـٰرَكَ"
        var result = engine.AlignToken("تبارك", words, 0);

        Assert.Equal(AlignmentOutcome.ExactMatch, result.Outcome);
        Assert.Equal(0, result.MatchedIndex);
        Assert.Empty(result.SkippedIndices);
    }

    [Fact]
    public void AlignmentEngine_DetectsForwardGapWhenWordSkipped()
    {
        var engine = new QuranAlignmentEngine();
        var (words, _) = MushafLayoutService.BuildPage562Layout(380f, 560f);

        // Find index of "لِيَبْلُوَكُمْ" and "أَيُّكُمْ" and "أَحْسَنُ" in Ayah 2
        int layabluwakumIdx = words.FindIndex(w => w.NormalizedText.Contains("ليبلوكم"));
        Assert.True(layabluwakumIdx >= 0);

        int ayyukumIdx = layabluwakumIdx + 1;
        int ahsanuIdx = layabluwakumIdx + 2;

        Assert.Contains("ايكم", words[ayyukumIdx].NormalizedText);
        Assert.Contains("احسن", words[ahsanuIdx].NormalizedText);

        // User recites "أَحْسَنُ", skipping "أَيُّكُمْ"
        var result = engine.AlignToken("احسن", words, ayyukumIdx);

        Assert.Equal(AlignmentOutcome.ForwardGap, result.Outcome);
        Assert.Equal(ahsanuIdx, result.MatchedIndex);
        Assert.Contains(ayyukumIdx, result.SkippedIndices);
    }

    [Fact]
    public void AlignmentEngine_DetectsBacktrackSelfCorrection()
    {
        var engine = new QuranAlignmentEngine();
        var (words, _) = MushafLayoutService.BuildPage562Layout(380f, 560f);

        int layabluwakumIdx = words.FindIndex(w => w.NormalizedText.Contains("ليبلوكم"));
        int ayyukumIdx = layabluwakumIdx + 1;
        int ahsanuIdx = layabluwakumIdx + 2;

        // Simulate forward gap: word ayyukumIdx was skipped, cursor is at ahsanuIdx + 1
        words[ayyukumIdx].Status = WordRecitationStatus.Skipped;
        words[ahsanuIdx].Status = WordRecitationStatus.Revealed;
        int currentCursor = ahsanuIdx + 1;

        // Reciter now backtracks and repeats "أَيُّكُمْ"
        var result = engine.AlignToken("ايكم", words, currentCursor);

        Assert.Equal(AlignmentOutcome.BacktrackCorrection, result.Outcome);
        Assert.Equal(ayyukumIdx, result.MatchedIndex);
    }

    [Fact]
    public void LayoutService_BuildsPage562WithPinnedAyahMarkers()
    {
        var (words, markers) = MushafLayoutService.BuildPage562Layout(380f, 560f);

        Assert.NotEmpty(words);
        Assert.NotEmpty(markers);

        // Verify Ayah markers are present for Page 562 (at least Ayahs 1 through 10)
        Assert.Contains(markers, m => m.AyahNumber == 1);
        Assert.Contains(markers, m => m.AyahNumber == 2);
        Assert.Contains(markers, m => m.AyahNumber == 3);
        Assert.Contains(markers, m => m.AyahNumber == 4);
        Assert.Contains(markers, m => m.AyahNumber == 5);
        Assert.Contains(markers, m => m.AyahNumber == 6);
        Assert.Contains(markers, m => m.AyahNumber == 7);
        Assert.Contains(markers, m => m.AyahNumber == 8);
        Assert.Contains(markers, m => m.AyahNumber == 9);
        Assert.Contains(markers, m => m.AyahNumber == 10);

        // Verify zero-shift coordinates are computed and non-zero
        foreach (var marker in markers)
        {
            Assert.True(marker.X > 0, $"Marker X should be positive for Ayah {marker.AyahNumber}");
            Assert.True(marker.Y > 0, $"Marker Y should be positive for Ayah {marker.AyahNumber}");
            Assert.True(marker.Radius > 5, "Marker radius should be reasonable");
        }

        foreach (var word in words)
        {
            Assert.True(word.X >= 0, $"Word X should be non-negative for {word.UthmaniText}");
            Assert.True(word.Y > 0, $"Word Y should be positive for {word.UthmaniText}");
            Assert.True(word.Width > 0, $"Word Width should be positive for {word.UthmaniText}");
            Assert.Equal(WordRecitationStatus.Hidden, word.Status);
        }
    }

    [Fact]
    public void LayoutService_Builds16LineLayout_ForArbitrarySurahVerses()
    {
        var verses = new List<Verse>
        {
            new() { SurahNum = 1, AyahNum = 1, ArabicText = "بِسْمِ ٱللَّهِ ٱلرَّحْمَـٰنِ ٱلرَّحِيمِ" },
            new() { SurahNum = 1, AyahNum = 2, ArabicText = "ٱلْحَمْدُ لِلَّهِ رَبِّ ٱلْعَـٰلَمِينَ" },
            new() { SurahNum = 1, AyahNum = 3, ArabicText = "ٱلرَّحْمَـٰنِ ٱلرَّحِيمِ" },
            new() { SurahNum = 1, AyahNum = 4, ArabicText = "مَـٰلِكِ يَوْمِ ٱلدِّينِ" },
            new() { SurahNum = 1, AyahNum = 5, ArabicText = "إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ" },
            new() { SurahNum = 1, AyahNum = 6, ArabicText = "ٱهْدِنَا ٱلصِّرَٰطَ ٱلْمُسْتَقِيمَ" },
            new() { SurahNum = 1, AyahNum = 7, ArabicText = "صِرَٰطَ ٱلَّذِينَ أَنْعَمْتَ عَلَيْهِمْ غَيْرِ ٱلْمَغْضُوبِ عَلَيْهِمْ وَلَا ٱلضَّآلِّينَ" }
        };

        var (words, markers) = MushafLayoutService.Build16LineLayout(verses, 1, 1, 380f, 560f);

        Assert.NotEmpty(words);
        Assert.Equal(7, markers.Count);
        for (int i = 1; i <= 7; i++)
        {
            Assert.Contains(markers, m => m.AyahNumber == i);
        }

        // Verify line count does not exceed 16 lines
        var distinctLines = words.Select(w => w.LineNumber).Distinct().ToList();
        Assert.True(distinctLines.Count <= 16, $"Line count {distinctLines.Count} should be <= 16");
    }

    [Fact]
    public void LayoutService_DistributesWordsAcross16LinesWithZeroShift()
    {
        // Test with 30-word Surah
        var verses = new List<Verse>
        {
            new() { SurahNum = 112, AyahNum = 1, ArabicText = "قُلْ هُوَ ٱللَّهُ أَحَدٌ" },
            new() { SurahNum = 112, AyahNum = 2, ArabicText = "ٱللَّهُ ٱلصَّمَدُ" },
            new() { SurahNum = 112, AyahNum = 3, ArabicText = "لَمْ يَلِدْ وَلَمْ يُولَدْ" },
            new() { SurahNum = 112, AyahNum = 4, ArabicText = "وَلَمْ يَكُن لَّهُۥ كُفُوًا أَحَدٌۢ" }
        };

        var (words, markers) = MushafLayoutService.Build16LineLayout(verses, 112, 604, 400f, 600f);

        Assert.NotEmpty(words);
        Assert.Equal(4, markers.Count);
        Assert.All(words, w => Assert.True(w.Width > 0));
        Assert.All(markers, m => Assert.True(m.Radius >= 10f));
    }
}
