namespace TarteelClone.LocalRecitationCore.Models;

public sealed record RecitationWordMismatch(
    int Position,
    string Spoken,
    string Expected);

public sealed class RecitationMatchResult
{
    public int SurahNum { get; init; }
    public int AyahNum { get; init; }
    public string ArabicText { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public IReadOnlyList<RecitationWordMismatch> Mismatches { get; init; } = [];
}

public sealed class RecitationVerse
{
    public int SurahNum { get; init; }
    public int AyahNum { get; init; }
    public string ArabicText { get; init; } = string.Empty;
    public string Translation { get; init; } = string.Empty;
}

public sealed record RecitationTranscriptionResult(
    bool IsSuccess,
    string Text,
    float Confidence,
    string TierUsed,
    bool UsedFallback,
    string? DiagnosticMessage = null)
{
    public static RecitationTranscriptionResult Failure(
        string message,
        string tierUsed,
        bool usedFallback = false) =>
        new(false, string.Empty, 0f, tierUsed, usedFallback, message);
}
