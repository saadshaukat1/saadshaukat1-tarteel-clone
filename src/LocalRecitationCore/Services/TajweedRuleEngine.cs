using System.Text;
using System.Text.RegularExpressions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Detects tajweed rule violations at the word level.
/// Rules implemented: Madd, Ghunna, Qalqalah, Idgham, Ikhfa, Iqlab, Izhar.
/// Operates on Arabic Unicode text; no external library required.
/// </summary>
public static class TajweedRuleEngine
{
    // ?? Qalqalah letters: ? ? ? ? ? ?????????????????????????????????????
    private static readonly HashSet<char> QalqalahLetters = new() { '?', '?', '?', '?', '?' };

    // ?? Ghunna source letters: ? (n?n) and ? (m?m) ???????????????????????
    private static readonly HashSet<char> GhunnaLetters = new() { '?', '?' };

    // ?? Idgham letters (n?n s?kin / tanw?n merges into these) ????????????
    private static readonly HashSet<char> IdghamLetters = new() { '?', '?', '?', '?', '?', '?' };

    // ?? Ikhfa letters (15 letters after n?n s?kin) ???????????????????????
    private static readonly HashSet<char> IkhfaLetters = new()
    {
        '?', '?', '?', '?', '?', '?', '?', '?', '?', '?', '?', '?', '?', '?', '?'
    };

    // ?? Izhar (throat) letters ????????????????????????????????????????????
    private static readonly HashSet<char> IzharLetters = new() { '?', '?', '?', '?', '?', '?' };

    // ?? Madd letters ?????????????????????????????????????????????????????
    private static readonly Regex MaddPattern = new(
        @"[???][\u064B-\u0652]?$",
        RegexOptions.Compiled);

    /// <summary>
    /// Analyses a list of expected words versus spoken words, identifies
    /// tajweed rule violations in the mismatch positions, and returns violations.
    /// </summary>
    public static IReadOnlyList<TajweedViolation> Analyze(
        IReadOnlyList<string> expectedWords,
        IReadOnlyList<string> spokenWords,
        IReadOnlyList<RecitationWordMismatch> mismatches)
    {
        if (expectedWords.Count == 0 || mismatches.Count == 0)
        {
            return [];
        }

        var violations = new List<TajweedViolation>();

        foreach (var mismatch in mismatches)
        {
            var pos = mismatch.Position;
            if (pos < 0 || pos >= expectedWords.Count)
            {
                continue;
            }

            var expected = expectedWords[pos];
            var spoken = mismatch.Spoken;

            // ?? 1. Madd check ?????????????????????????????????????????????
            if (HasMadd(expected) && !HasMadd(spoken))
            {
                violations.Add(new TajweedViolation(
                    pos, TajweedRuleType.Madd, expected, spoken,
                    $"The letter in «{expected}» requires elongation (madd). Extend the vowel for 2–6 beats."));
                continue; // one rule per mismatch is sufficient
            }

            // ?? 2. N?n / M?m rules (Ghunna, Idgham, Ikhfa, Iqlab, Izhar) ?
            if (pos + 1 < expectedWords.Count)
            {
                var nextWord = expectedWords[pos + 1];
                var nunMimRule = ClassifyNunMimRule(expected, nextWord);
                if (nunMimRule.HasValue)
                {
                    var hint = BuildNunMimHint(nunMimRule.Value, expected, nextWord);
                    violations.Add(new TajweedViolation(pos, nunMimRule.Value, expected, spoken, hint));
                    continue;
                }
            }

            // ?? 3. Qalqalah check ????????????????????????????????????????
            if (HasQalqalahAtEnd(expected))
            {
                violations.Add(new TajweedViolation(
                    pos, TajweedRuleType.Qalqalah, expected, spoken,
                    $"«{expected}» ends on a Qalqalah letter. Apply a short echo/bounce when stopping."));
                continue;
            }

            // ?? 4. Ghunna on isolated n?n or m?m ?????????????????????????
            if (HasStandAloneGhunna(expected))
            {
                violations.Add(new TajweedViolation(
                    pos, TajweedRuleType.Ghunna, expected, spoken,
                    $"«{expected}» contains a shaddah on n?n or m?m. Apply 2-beat nasalization."));
            }
        }

        return violations;
    }

    // ?? Private helpers ???????????????????????????????????????????????????

    private static bool HasMadd(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        return MaddPattern.IsMatch(Strip(word));
    }

    private static bool HasQalqalahAtEnd(string word)
    {
        var stripped = Strip(word);
        if (stripped.Length == 0)
        {
            return false;
        }

        // Qalqalah applies when the qalqalah letter is s?kin (no vowel) or at a stop.
        var last = stripped[^1];
        return QalqalahLetters.Contains(last);
    }

    private static bool HasStandAloneGhunna(string word)
    {
        // Shaddah (? \u0651) on n?n or m?m requires ghunna.
        for (var i = 1; i < word.Length; i++)
        {
            if (word[i] == '\u0651' && GhunnaLetters.Contains(word[i - 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static TajweedRuleType? ClassifyNunMimRule(string currentWord, string nextWord)
    {
        var stripped = Strip(currentWord);
        if (stripped.Length == 0 || string.IsNullOrWhiteSpace(nextWord))
        {
            return null;
        }

        var lastLetter = stripped[^1];
        if (lastLetter != '?')
        {
            return null; // Only n?n s?kin/tanw?n triggers these rules
        }

        var nextFirst = Strip(nextWord).Length > 0 ? Strip(nextWord)[0] : '\0';
        if (nextFirst == '\0')
        {
            return null;
        }

        // Iqlab: n?n before ??
        if (nextFirst == '?')
        {
            return TajweedRuleType.Iqlab;
        }

        // Idgham: n?n before ? ? ? ? ? ?
        if (IdghamLetters.Contains(nextFirst))
        {
            return TajweedRuleType.Idgham;
        }

        // Izhar: n?n before throat letters
        if (IzharLetters.Contains(nextFirst))
        {
            return TajweedRuleType.Izhar;
        }

        // Ikhfa: n?n before any of the 15 ikhfa letters
        if (IkhfaLetters.Contains(nextFirst))
        {
            return TajweedRuleType.Ikhfa;
        }

        return null;
    }

    private static string BuildNunMimHint(TajweedRuleType rule, string current, string next) => rule switch
    {
        TajweedRuleType.Iqlab  => $"«{current}» before «{next}» — change n?n to m?m sound (iqlab) with nasal.",
        TajweedRuleType.Idgham => $"«{current}» before «{next}» — merge (idgham) the n?n into the following letter.",
        TajweedRuleType.Izhar  => $"«{current}» before «{next}» — pronounce n?n clearly with no nasalization (izhar).",
        TajweedRuleType.Ikhfa  => $"«{current}» before «{next}» — partially hide n?n with gentle nasalization (ikhfa).",
        _                      => string.Empty
    };

    /// <summary>Strips Arabic diacritics (harakat) for consonant-level comparison.</summary>
    private static string Strip(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(word.Length);
        foreach (var ch in word)
        {
            // Skip diacritics U+064B–U+065F (harakat range)
            if (ch is >= '\u064B' and <= '\u065F')
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
