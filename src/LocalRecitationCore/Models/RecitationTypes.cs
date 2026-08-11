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
        TajweedRuleType.Madd => "Madd · elongate",
        TajweedRuleType.Ghunna => "Ghunna · nasalization",
        TajweedRuleType.Qalqalah => "Qalqalah · echo",
        TajweedRuleType.Idgham => "Idgham · merge",
        TajweedRuleType.Ikhfa => "Ikhfa · conceal",
        TajweedRuleType.Iqlab => "Iqlab · convert",
        TajweedRuleType.Izhar => "Izhar · clarify",
        _ => "Makhraj · articulation",
    };
}

public sealed record TajweedErrorRecord(
    TajweedRuleType Rule,
    int SurahNum,
    int AyahNum,
    int ErrorCount,
    DateTimeOffset LastAttemptedAt)
{
    public string RuleDisplayName => Rule switch
    {
        TajweedRuleType.Madd => "Madd",
        TajweedRuleType.Ghunna => "Ghunna",
        TajweedRuleType.Qalqalah => "Qalqalah",
        TajweedRuleType.Idgham => "Idgham",
        TajweedRuleType.Ikhfa => "Ikhfa",
        TajweedRuleType.Iqlab => "Iqlab",
        TajweedRuleType.Izhar => "Izhar",
        _ => "Makhraj",
    };
}

public sealed record TajweedRuleSummary(
    TajweedRuleType Rule,
    int TotalErrors,
    int AffectedVerseCount,
    DateTimeOffset LastErrorAt);

public sealed class RecitationMatchResult
{
    public int SurahNum { get; init; }
    public int AyahNum { get; init; }
    public string ArabicText { get; init; } = string.Empty;
    public string TranscriptionText { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public int ProcessedWordCount { get; init; }
    public int MatchedWordCount { get; init; }
    public int JuzNum { get; init; }
    public string SurahNameEnglish { get; init; } = string.Empty;
    public string SurahNameArabic { get; init; } = string.Empty;
    public IReadOnlyList<RecitationWordMismatch> Mismatches { get; init; } = [];
    public IReadOnlyList<TajweedViolation> TajweedViolations { get; init; } = [];
    /// <summary>
    /// For each spoken word (by index), the aligned expected-word position or null
    /// when the spoken word is an insertion with no expected counterpart. The count
    /// matches the number of transcribed spoken words. Used by the phoneme analyzer
    /// to pair real timestamps with their true ayah-word positions.
    /// </summary>
    public IReadOnlyList<int?> SpokenToExpectedPosition { get; init; } = [];
}
public sealed class RecitationVerse
{
    public int SurahNum { get; init; }
    public int AyahNum { get; init; }
    public string ArabicText { get; init; } = string.Empty;
    public string Translation { get; init; } = string.Empty;
}

public sealed record RecitationWordTimestamp(
    int Index,
    string Word,
    TimeSpan Start,
    TimeSpan End);

public sealed record RecitationTranscriptionResult(
    bool IsSuccess,
    string Text,
    float Confidence,
    string TierUsed,
    bool UsedFallback,
    string? DiagnosticMessage = null)
{
    public IReadOnlyList<RecitationWordTimestamp> WordTimestamps { get; init; } = [];

    public static RecitationTranscriptionResult Failure(
        string message,
        string tierUsed,
        bool usedFallback = false) =>
        new(false, string.Empty, 0f, tierUsed, usedFallback, message);
}
