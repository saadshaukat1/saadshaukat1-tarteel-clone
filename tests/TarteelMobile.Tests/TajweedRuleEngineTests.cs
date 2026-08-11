using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Services;
using Xunit;

namespace TarteelMobile.Tests;

public sealed class TajweedRuleEngineTests
{
    public static IEnumerable<object[]> NunMimRules()
    {
        yield return [TajweedRuleType.Idgham, "مِنْ", "يَقُولُ"];
        yield return [TajweedRuleType.Ikhfa, "مِنْ", "تَحْتِ"];
        yield return [TajweedRuleType.Iqlab, "مِنْ", "بَعْدِ"];
        yield return [TajweedRuleType.Izhar, "مِنْ", "هَادٍ"];
    }

    [Theory]
    [MemberData(nameof(NunMimRules))]
    public void Analyze_ClassifiesNunMimRules(TajweedRuleType expectedRule, string current, string next)
    {
        var violations = TajweedRuleEngine.Analyze(
            [current, next],
            ["خطأ", next],
            [new RecitationWordMismatch(0, "خطأ", current)]);

        var violation = Assert.Single(violations);
        Assert.Equal(expectedRule, violation.Rule);
        Assert.Contains(expectedRule.ToString(), violation.RuleDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_DetectsMaddWhenSpokenWordLosesMaddLetter()
    {
        var violations = TajweedRuleEngine.Analyze(
            ["فِي"],
            ["فِ"],
            [new RecitationWordMismatch(0, "فِ", "فِي")]);

        Assert.Equal(TajweedRuleType.Madd, Assert.Single(violations).Rule);
    }

    [Fact]
    public void Analyze_DetectsQalqalahAtWordEnd()
    {
        var violations = TajweedRuleEngine.Analyze(
            ["أَحَدْ"],
            ["أَحَذْ"],
            [new RecitationWordMismatch(0, "أَحَذْ", "أَحَدْ")]);

        Assert.Equal(TajweedRuleType.Qalqalah, Assert.Single(violations).Rule);
    }

    [Fact]
    public void Analyze_DetectsGhunnaOnShaddah()
    {
        var violations = TajweedRuleEngine.Analyze(
            ["إِنَّ"],
            ["إِنَ"],
            [new RecitationWordMismatch(0, "إِنَ", "إِنَّ")]);

        Assert.Equal(TajweedRuleType.Ghunna, Assert.Single(violations).Rule);
    }
}
