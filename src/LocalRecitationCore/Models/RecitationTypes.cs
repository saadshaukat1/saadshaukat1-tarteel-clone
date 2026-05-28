namespace TarteelClone.LocalRecitationCore.Models;

public sealed record RecitationWordMismatch(
    int Position,
    string Spoken,
    string Expected);

public enum TajweedRuleType
{
    Madd,
    Ghunna,
    Qalqalah,
    Idgham,
    Ikhfa,
    Iqlab,
    Izhar,
    MakhrajError,
}

public sealed record TajweedViolation(
    int WordPosition,
    TajweedRuleType Rule,
    string ExpectedWord,
    string SpokenWord,
    string Hint)
{
    public string RuleDisplayName => Rule switch
    {
        TajweedRuleType.Madd        => "?? Madd — Elongate this letter",
        TajweedRuleType.Ghunna      => "?? Ghunna — Apply nasalization (2 counts)",
        TajweedRuleType.Qalqalah    => "?? Qalqalah — Add echo on plosive letter",
        TajweedRuleType.Idgham      => "?? Idgham — Merge n?n into following letter",
        TajweedRuleType.Ikhfa       => "?? Ikhfa — Hide n?n with partial nasalization",
        TajweedRuleType.Iqlab       => "?? Iqlab — Change n?n to m?m before b?'",
        TajweedRuleType.Izhar       => "? Izhar — Pronounce n?n clearly",
        _                           => "? Makhraj — Check articulation point",
    };
}

public sealed class RecitationMatchResult
{
    public int SurahNum { get; init; }
    public int AyahNum { get; init; }
    public string ArabicText { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public int ProcessedWordCount { get; init; }
    public int MatchedWordCount { get; init; }
    public IReadOnlyList<RecitationWordMismatch> Mismatches { get; init; } = [];
    public IReadOnlyList<TajweedViolation> TajweedViolations { get; init; } = [];
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
