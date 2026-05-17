using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Baseline matcher used until full matcher extraction is completed.
/// </summary>
public sealed class PlaceholderVerseMatcher : IVerseMatcher
{
    private readonly IVerseRepository _verses;

    public PlaceholderVerseMatcher(IVerseRepository verses)
    {
        _verses = verses;
    }

    public async Task<RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default)
    {
        var verse = await _verses.GetVerseAsync(1, 1, cancellationToken);
        if (verse is null)
        {
            return new RecitationMatchResult
            {
                Confidence = 0
            };
        }

        return new RecitationMatchResult
        {
            SurahNum = verse.SurahNum,
            AyahNum = verse.AyahNum,
            ArabicText = string.IsNullOrWhiteSpace(arabicText) ? verse.ArabicText : arabicText,
            Confidence = string.IsNullOrWhiteSpace(arabicText) ? 0.5 : 0.8,
            Mismatches = []
        };
    }
}
