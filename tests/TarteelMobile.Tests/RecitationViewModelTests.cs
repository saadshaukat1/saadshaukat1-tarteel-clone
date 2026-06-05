using TarteelMobile.ViewModels;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class RecitationViewModelTests
{
    [Fact]
    public void BuildAyahNumbers_UsesValidRangesPerSurah()
    {
        var fatiha = RecitationPracticeSettings.BuildAyahNumbers(1);
        var baqarah = RecitationPracticeSettings.BuildAyahNumbers(2);
        var ikhlas = RecitationPracticeSettings.BuildAyahNumbers(112);

        Assert.Equal(7, fatiha.Count);
        Assert.Equal(1, fatiha.First());
        Assert.Equal(7, fatiha.Last());

        Assert.Equal(286, baqarah.Count);
        Assert.Equal(1, baqarah.First());
        Assert.Equal(286, baqarah.Last());

        Assert.Equal(4, ikhlas.Count);
    }

    [Fact]
    public void ClampAyah_KeepsSelectionInsideSurahBounds()
    {
        Assert.Equal(4, RecitationPracticeSettings.ClampAyah(286, 112));
        Assert.Equal(1, RecitationPracticeSettings.ClampAyah(0, 1));
        Assert.Equal(200, RecitationPracticeSettings.ClampAyah(200, 3));
    }

    [Fact]
    public void AdvancedPanelVisibility_RequiresAdvancedModeAndPanelState()
    {
        Assert.False(RecitationPracticeSettings.ShouldShowAdvancedPanel(false, true));
        Assert.False(RecitationPracticeSettings.ShouldShowAdvancedPanel(true, false));
        Assert.True(RecitationPracticeSettings.ShouldShowAdvancedPanel(true, true));
    }
}
