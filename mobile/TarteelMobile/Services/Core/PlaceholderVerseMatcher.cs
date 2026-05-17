using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Core;

/// <summary>
/// Baseline matcher for local desktop flow until QuranEngine extraction lands.
/// </summary>
public sealed class PlaceholderVerseMatcher : CoreAbstractions.IVerseMatcher
{
    private readonly CoreAbstractions.IVerseRepository _verseRepository;

    public PlaceholderVerseMatcher(CoreAbstractions.IVerseRepository verseRepository)
    {
        _verseRepository = verseRepository;
    }

    public async Task<CoreModels.RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default)
    {
        var verse = await _verseRepository.GetVerseAsync(1, 1, cancellationToken);
        if (verse is null)
        {
            return new CoreModels.RecitationMatchResult { Confidence = 0 };
        }

        var text = string.IsNullOrWhiteSpace(arabicText) ? verse.ArabicText : arabicText;
        return new CoreModels.RecitationMatchResult
        {
            SurahNum = verse.SurahNum,
            AyahNum = verse.AyahNum,
            ArabicText = text,
            Confidence = string.IsNullOrWhiteSpace(arabicText) ? 0.5 : 0.8,
            Mismatches = []
        };
    }
}
