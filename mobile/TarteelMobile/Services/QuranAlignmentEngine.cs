using TarteelMobile.Models;

namespace TarteelMobile.Services;

public enum AlignmentOutcome
{
    ExactMatch,
    ForwardGap,
    BacktrackCorrection,
    NoMatch
}

public class AlignmentResult
{
    public AlignmentOutcome Outcome { get; set; } = AlignmentOutcome.NoMatch;
    public int MatchedIndex { get; set; }
    public List<int> SkippedIndices { get; set; } = [];
}

public class QuranAlignmentEngine
{
    public static int ComputeLevenshtein(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private static bool IsFuzzyMatch(string spokenNorm, string expectedNorm)
    {
        if (string.IsNullOrEmpty(spokenNorm) || string.IsNullOrEmpty(expectedNorm))
            return false;

        if (spokenNorm == expectedNorm)
            return true;

        var dist = ComputeLevenshtein(spokenNorm, expectedNorm);
        // Short words (<= 3 chars) allow 1 edit; longer words (>= 6 chars) can tolerate 2 edits
        int maxAllowed = expectedNorm.Length >= 6 ? 2 : 1;
        return dist <= maxAllowed;
    }

    public AlignmentResult AlignToken(
        string recognizedToken,
        IList<QuranWordModel> words,
        int currentIndex,
        int lookaheadWindow = 4)
    {
        string norm = ArabicTextNormalizer.Normalize(recognizedToken);
        var result = new AlignmentResult { Outcome = AlignmentOutcome.NoMatch, MatchedIndex = currentIndex };

        if (string.IsNullOrEmpty(norm) || words.Count == 0 || currentIndex >= words.Count)
            return result;

        // 1. Direct Next Word Check
        if (IsFuzzyMatch(norm, words[currentIndex].NormalizedText))
        {
            result.Outcome = AlignmentOutcome.ExactMatch;
            result.MatchedIndex = currentIndex;
            return result;
        }

        // 2. Forward Lookahead (Gap / Skipped Word)
        int maxLookahead = Math.Min(currentIndex + lookaheadWindow, words.Count);
        for (int i = currentIndex + 1; i < maxLookahead; i++)
        {
            if (IsFuzzyMatch(norm, words[i].NormalizedText))
            {
                result.Outcome = AlignmentOutcome.ForwardGap;
                result.MatchedIndex = i;
                for (int s = currentIndex; s < i; s++)
                {
                    result.SkippedIndices.Add(s);
                }
                return result;
            }
        }

        // 3. Backtrack Check (Self-Correction for previously skipped or mistaken words)
        int minLookback = Math.Max(0, currentIndex - 4);
        for (int b = currentIndex - 1; b >= minLookback; b--)
        {
            if (IsFuzzyMatch(norm, words[b].NormalizedText))
            {
                result.Outcome = AlignmentOutcome.BacktrackCorrection;
                result.MatchedIndex = b;
                return result;
            }
        }

        return result;
    }

    public List<AlignmentResult> AlignPhrase(
        string recognizedPhrase,
        IList<QuranWordModel> words,
        ref int currentIndex,
        int lookaheadWindow = 4)
    {
        var tokens = ArabicTextNormalizer.TokenizeAndNormalize(recognizedPhrase);
        var results = new List<AlignmentResult>();

        foreach (var token in tokens)
        {
            if (currentIndex >= words.Count)
                break;

            var match = AlignToken(token, words, currentIndex, lookaheadWindow);
            results.Add(match);

            switch (match.Outcome)
            {
                case AlignmentOutcome.ExactMatch:
                    words[match.MatchedIndex].Status = WordRecitationStatus.Revealed;
                    currentIndex = match.MatchedIndex + 1;
                    break;

                case AlignmentOutcome.ForwardGap:
                    foreach (var s in match.SkippedIndices)
                    {
                        words[s].Status = WordRecitationStatus.Skipped;
                    }
                    words[match.MatchedIndex].Status = WordRecitationStatus.Revealed;
                    currentIndex = match.MatchedIndex + 1;
                    break;

                case AlignmentOutcome.BacktrackCorrection:
                    words[match.MatchedIndex].Status = WordRecitationStatus.Corrected;
                    // Do not rewind cursor if it was already past this word
                    break;
            }
        }

        return results;
    }
}
