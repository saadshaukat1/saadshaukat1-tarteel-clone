namespace TarteelClone.QuranEngine.Models;

/// <summary>Result returned from verse matching against a transcribed Arabic string.</summary>
public class MatchResult
{
    public int    SurahNum    { get; init; }
    public int    AyahNum     { get; init; }
    public string ArabicText  { get; init; } = string.Empty;
    public double Confidence  { get; init; }

    /// <summary>Words in the transcription that differ from the canonical text.</summary>
    public IReadOnlyList<WordMismatch> Mismatches { get; init; } = [];
}

public record WordMismatch(
    int    Position,
    string Spoken,
    string Expected);
