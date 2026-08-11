using Microsoft.Maui.Graphics;

namespace TarteelMobile.Models;

/// <summary>A single Quran verse loaded from local offline repository data.</summary>
public class Verse
{
    public int    SurahNum    { get; set; }
    public int    AyahNum     { get; set; }
    public string ArabicText  { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

/// <summary>Memorization progress record for a single verse.</summary>
public sealed record VerseProgress(
    int SurahNum,
    int AyahNum,
    string ArabicText,
    double MasteryScore,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? NextReviewAt = null,
    int AttemptCount = 0,
    int RecentErrorCount = 0)
{
    public string Label => $"{SurahNum}:{AyahNum}";
    public string MasteryDisplay => $"{MasteryScore:P0}";
    public Color MasteryColor => MasteryScore switch
    {
        >= 0.9 => (Color)Application.Current!.Resources["Success"],
        >= 0.6 => (Color)Application.Current!.Resources["Warning"],
        _       => (Color)Application.Current!.Resources["Error"]
    };
    public string UpdatedDisplay => UpdatedAt == DateTimeOffset.MinValue
        ? string.Empty
        : UpdatedAt.LocalDateTime.ToString("MMM d");
}

