namespace TarteelMobile.Models;

/// <summary>
/// Describes a single Mushaf page in the standard 604-page Madani layout:
/// the contiguous verse range rendered on that page.
/// </summary>
public sealed record MushafPage(
    int PageNum,
    int StartSurah,
    int StartAyah,
    int EndSurah,
    int EndAyah)
{
    public string Label => $"Page {PageNum}";
    public string VerseRangeLabel => StartSurah == EndSurah
        ? $"{StartSurah}:{StartAyah}–{EndAyah}"
        : $"{StartSurah}:{StartAyah} – {EndSurah}:{EndAyah}";
}
