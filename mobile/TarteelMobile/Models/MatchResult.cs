using CoreModels = TarteelClone.LocalRecitationCore.Models;

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
    public int JuzNum { get; set; }
    public string SurahNameEnglish { get; set; } = string.Empty;
    public string SurahNameArabic { get; set; } = string.Empty;
    public List<WordMismatch> Mismatches { get; set; } = [];
    public List<TajweedViolation> TajweedViolations { get; set; } = [];
}

public record WordMismatch(int Position, string Spoken, string Expected);

public sealed record TajweedViolation(
    int WordPosition,
    CoreModels.TajweedRuleType Rule,
    string ExpectedWord,
    string SpokenWord,
    string Hint,
    string RuleDisplayName);

