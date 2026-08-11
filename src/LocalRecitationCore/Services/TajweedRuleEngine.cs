using System.Text;
using System.Text.RegularExpressions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

public static class TajweedRuleEngine
{
    private static readonly HashSet<char> QalqalahLetters = new() { '\u0642', '\u0637', '\u0628', '\u062C', '\u062F' };
    private static readonly HashSet<char> GhunnaLetters = new() { '\u0646', '\u0645' };
    private static readonly HashSet<char> IdghamLetters = new() { '\u064A', '\u0631', '\u0645', '\u0644', '\u0648', '\u0646' };
    private static readonly HashSet<char> IkhfaLetters = new()
    {
        '\u062A', '\u062B', '\u062C', '\u062F', '\u0630', '\u0632', '\u0633', '\u0634',
        '\u0635', '\u0636', '\u0637', '\u0638', '\u0641', '\u0642', '\u0643'
    };
    private static readonly HashSet<char> IzharLetters = new() { '\u0621', '\u0647', '\u0639', '\u062D', '\u063A', '\u062E' };
    private static readonly Regex MaddPattern = new(@"[\u0627\u0648\u064A][\u064B-\u0652]?$", RegexOptions.Compiled);

    public static IReadOnlyList<TajweedViolation> Analyze(
        IReadOnlyList<string> expectedWords,
        IReadOnlyList<string> spokenWords,
        IReadOnlyList<RecitationWordMismatch> mismatches)
    {
        if (expectedWords.Count == 0)
        {
            return [];
        }

        var violationSet = new HashSet<(int Position, TajweedRuleType Rule)>();

        var violations = new List<TajweedViolation>();

        void AddUnique(TajweedViolation violation)
        {
            if (violationSet.Add((violation.WordPosition, violation.Rule)))
                violations.Add(violation);
        }

        // Process mismatches with the full priority chain (unchanged logic).
        foreach (var mismatch in mismatches)
        {
            var position = mismatch.Position;
            if (position < 0 || position >= expectedWords.Count)
                continue;

            var expected = expectedWords[position];
            var spoken = mismatch.Spoken;
            if (HasMadd(expected) && !HasMadd(spoken))
            {
                AddUnique(new TajweedViolation(
                    position, TajweedRuleType.Madd, expected, spoken,
                    $"{expected} requires elongation. Extend the vowel for 2–6 counts."));
                continue;
            }

            if (position + 1 < expectedWords.Count)
            {
                var rule = ClassifyNunMimRule(expected, expectedWords[position + 1]);
                if (rule.HasValue)
                {
                    AddUnique(new TajweedViolation(
                        position,
                        rule.Value,
                        expected,
                        spoken,
                        BuildNunMimHint(rule.Value, expected, expectedWords[position + 1])));
                    continue;
                }
            }

            if (HasQalqalahAtEnd(expected))
            {
                AddUnique(new TajweedViolation(
                    position, TajweedRuleType.Qalqalah, expected, spoken,
                    $"{expected} ends on a Qalqalah letter. Add a short echo when stopping."));
                continue;
            }

            if (HasStandAloneGhunna(expected))
            {
                AddUnique(new TajweedViolation(
                    position, TajweedRuleType.Ghunna, expected, spoken,
                    $"{expected} contains a shaddah on nūn or mīm. Apply 2 counts of nasalization."));
            }
        }

        // Check all words for letter-presence tajweed traits regardless of match state.
        // This catches correctly-transcribed words that still have tajweed features.
        for (var pos = 0; pos < expectedWords.Count; pos++)
        {
            var expected = expectedWords[pos];
            if (HasMadd(expected) && violationSet.Add((pos, TajweedRuleType.Madd)))
            {
                violations.Add(new TajweedViolation(
                    pos, TajweedRuleType.Madd, expected, expected,
                    $"Madd letter in '{expected}' — extend 2–6 counts."));
            }

            if (HasStandAloneGhunna(expected) && violationSet.Add((pos, TajweedRuleType.Ghunna)))
            {
                violations.Add(new TajweedViolation(
                    pos, TajweedRuleType.Ghunna, expected, expected,
                    $"Shaddah on nūn/mīm in '{expected}' — apply 2 counts of nasalization."));
            }

            if (pos + 1 < expectedWords.Count)
            {
                var rule = ClassifyNunMimRule(expected, expectedWords[pos + 1]);
                if (rule.HasValue && violationSet.Add((pos, rule.Value)))
                {
                    violations.Add(new TajweedViolation(
                        pos,
                        rule.Value,
                        expected,
                        expected,
                        BuildNunMimHint(rule.Value, expected, expectedWords[pos + 1])));
                }
            }
        }

        return violations;
    }

    private static bool HasMadd(string word) =>
        !string.IsNullOrWhiteSpace(word) && MaddPattern.IsMatch(Strip(word));

    private static bool HasQalqalahAtEnd(string word)
    {
        var stripped = Strip(word);
        return stripped.Length > 0 && QalqalahLetters.Contains(stripped[^1]);
    }

    private static bool HasStandAloneGhunna(string word)
    {
        var previousBaseLetter = '\0';
        foreach (var character in word)
        {
            if (character is >= '\u064B' and <= '\u065F')
            {
                if (character == '\u0651' && GhunnaLetters.Contains(previousBaseLetter))
                {
                    return true;
                }

                continue;
            }

            previousBaseLetter = character;
        }

        return false;
    }

    private static TajweedRuleType? ClassifyNunMimRule(string currentWord, string nextWord)
    {
        var current = Strip(currentWord);
        var next = Strip(nextWord);
        if (current.Length == 0 || next.Length == 0 || current[^1] != '\u0646')
        {
            return null;
        }

        return next[0] switch
        {
            '\u0628' => TajweedRuleType.Iqlab,
            _ when IdghamLetters.Contains(next[0]) => TajweedRuleType.Idgham,
            _ when IzharLetters.Contains(next[0]) => TajweedRuleType.Izhar,
            _ when IkhfaLetters.Contains(next[0]) => TajweedRuleType.Ikhfa,
            _ => null
        };
    }

    private static string BuildNunMimHint(TajweedRuleType rule, string current, string next) => rule switch
    {
        TajweedRuleType.Iqlab => $"{current} before {next}: change nūn to a mīm sound with nasalization.",
        TajweedRuleType.Idgham => $"{current} before {next}: merge the nūn into the following letter.",
        TajweedRuleType.Izhar => $"{current} before {next}: pronounce the nūn clearly without merging.",
        TajweedRuleType.Ikhfa => $"{current} before {next}: partially hide the nūn with gentle nasalization.",
        _ => string.Empty
    };

    internal static string Strip(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(word.Length);
        foreach (var character in word)
        {
            if (character is >= '\u064B' and <= '\u065F')
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
