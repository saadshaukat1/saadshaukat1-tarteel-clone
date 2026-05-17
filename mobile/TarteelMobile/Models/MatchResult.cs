namespace TarteelMobile.Models;

/// <summary>Real-time match result emitted by the local recitation pipeline.</summary>
public class MatchResult
{
    public int    SurahNum   { get; set; }
    public int    AyahNum    { get; set; }
    public string ArabicText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int ProcessedWordCount { get; set; }
    public int MatchedWordCount { get; set; }
    public List<WordMismatch> Mismatches { get; set; } = [];
}

public record WordMismatch(int Position, string Spoken, string Expected);
