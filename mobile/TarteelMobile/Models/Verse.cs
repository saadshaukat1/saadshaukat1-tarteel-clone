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
    DateTimeOffset UpdatedAt)
{
    public string Label => $"{SurahNum}:{AyahNum}";
    public string MasteryDisplay => $"{MasteryScore:P0}";
    public Color MasteryColor => MasteryScore switch
    {
        >= 0.9 => Color.FromArgb("#1A6B3C"),
        >= 0.6 => Color.FromArgb("#F59E0B"),
        _       => Color.FromArgb("#E53935")
    };
    public string UpdatedDisplay => UpdatedAt == DateTimeOffset.MinValue
        ? string.Empty
        : UpdatedAt.LocalDateTime.ToString("MMM d");
}

