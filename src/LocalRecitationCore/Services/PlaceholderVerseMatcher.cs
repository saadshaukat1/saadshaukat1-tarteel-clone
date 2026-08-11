using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Utilities;

namespace TarteelClone.LocalRecitationCore.Services;

public sealed class PlaceholderVerseMatcher : IVerseMatcher
{
    private readonly IVerseRepository _verses;
    private int? _surahContext;
    private int? _lastSurahNum;
    private int? _lastAyahNum;
    private sealed record WordToken(string Original, string Normalized);

    public PlaceholderVerseMatcher(IVerseRepository verses)
    {
        _verses = verses;
    }

    public void SetSurahContext(int surahNumber)
    {
        _surahContext = surahNumber >= 1 && surahNumber <= 114 ? surahNumber : null;
    }

    public void ClearSurahContext()
    {
        _surahContext = null;
    }

    public void SetLastMatchedPosition(int surahNum, int ayahNum)
    {
        _lastSurahNum = surahNum >= 1 && surahNum <= 114 ? surahNum : null;
        _lastAyahNum = ayahNum >= 1 ? ayahNum : null;
    }

    public void ClearLastMatchedPosition()
    {
        _lastSurahNum = null;
        _lastAyahNum = null;
    }

    public async Task<RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default)
    {
        var spokenTokens = Tokenize(arabicText);
        if (spokenTokens.Count == 0)
        {
            return new RecitationMatchResult { Confidence = 0 };
        }

        var contentWords = spokenTokens
            .Select(t => t.Normalized)
            .Where(w => w.Length > 1)
            .Distinct()
            .ToList();

        IReadOnlyList<RecitationVerse> candidates;
        if (contentWords.Count > 0)
        {
            candidates = await _verses.GetCandidateVersesAsync(contentWords, cancellationToken);
            if (candidates.Count < 10)
            {
                var fullCandidates = await _verses.GetAllVersesAsync(cancellationToken);
                if (fullCandidates.Count > candidates.Count)
                {
                    candidates = fullCandidates;
                }
            }
        }
        else
        {
            candidates = await _verses.GetAllVersesAsync(cancellationToken);
        }

        // Constrain candidates to the active surah range when SetSurahContext was called.
        if (_surahContext.HasValue)
        {
            var filtered = candidates.Where(v => v.SurahNum == _surahContext.Value).ToList();
            if (filtered.Count > 0)
            {
                candidates = filtered;
            }
        }

        if (candidates.Count == 0)
        {
            return new RecitationMatchResult { Confidence = 0 };
        }

        RecitationMatchResult? bestResult = null;
        var bestAdjustedConfidence = double.MinValue;
        var bestRawConfidence = 0.0;

        foreach (var candidate in candidates)
        {
            var expectedTokens = Tokenize(candidate.ArabicText);
            if (expectedTokens.Count == 0)
            {
                continue;
            }

            var (matchedWords, processedWords, mismatches, spokenToExpected) = AlignTokens(expectedTokens, spokenTokens);
            var rawConfidence = ComputeConfidence(expectedTokens, spokenTokens, matchedWords);
            var positionMultiplier = ComputePositionMultiplier(candidate.SurahNum, candidate.AyahNum);
            var adjustedConfidence = Math.Clamp(rawConfidence * positionMultiplier, 0, 1);

            if (adjustedConfidence > bestAdjustedConfidence)
            {
                bestAdjustedConfidence = adjustedConfidence;
                bestRawConfidence = rawConfidence;
                var expectedWords = expectedTokens.Select(t => t.Original).ToList();
                var spokenWords   = spokenTokens.Select(t => t.Original).ToList();
                var tajweedViolations = TajweedRuleEngine.Analyze(expectedWords, spokenWords, mismatches);

                bestResult = new RecitationMatchResult
                {
                    SurahNum = candidate.SurahNum,
                    AyahNum = candidate.AyahNum,
                    ArabicText = candidate.ArabicText,
                    Confidence = adjustedConfidence,
                    ProcessedWordCount = processedWords,
                    MatchedWordCount = matchedWords,
                    Mismatches = mismatches,
                    TajweedViolations = tajweedViolations,
                    SpokenToExpectedPosition = spokenToExpected
                };
            }
        }

        if (bestResult is null)
        {
            var first = candidates[0];
            return new RecitationMatchResult
            {
                SurahNum = first.SurahNum,
                AyahNum = first.AyahNum,
                ArabicText = string.Empty,
                Confidence = 0,
                Mismatches = []
            };
        }

        return bestResult;
    }

    private double ComputePositionMultiplier(int candidateSurah, int candidateAyah)
    {
        if (!_lastSurahNum.HasValue || !_lastAyahNum.HasValue)
        {
            return 1.0;
        }

        if (candidateSurah != _lastSurahNum.Value)
        {
            return 0.88;
        }

        var delta = candidateAyah - _lastAyahNum.Value;
        if (delta < 0)
        {
            return 0.92;
        }

        if (delta == 0)
        {
            return 1.0;
        }

        if (delta == 1)
        {
            return 1.03;
        }

        if (delta <= 3)
        {
            return 0.98;
        }

        return 0.95;
    }

    private static IReadOnlyList<WordToken> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tokens = new List<WordToken>(parts.Length);
        foreach (var part in parts)
        {
            var normalized = ArabicNormalizer.Normalize(part);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            tokens.Add(new WordToken(part, normalized));
        }

        return tokens;
    }

    private const double GapExtendPenalty = -0.5;
    private const double GapOpenPenalty = -1.2;

    private static (int MatchedWords, int ProcessedWords, IReadOnlyList<RecitationWordMismatch> Mismatches, IReadOnlyList<int?> SpokenToExpected) AlignTokens(
        IReadOnlyList<WordToken> expectedTokens,
        IReadOnlyList<WordToken> spokenTokens)
    {
        var eLen = expectedTokens.Count;
        var sLen = spokenTokens.Count;

        if (eLen == 0 || sLen == 0)
            return (0, 0, [], new int?[sLen]);

        var score = new double[eLen + 1, sLen + 1];
        var trace = new byte[eLen + 1, sLen + 1];

        for (var i = 0; i <= eLen; i++) { score[i, 0] = GapOpenPenalty + (i - 1) * GapExtendPenalty; trace[i, 0] = 3; }
        for (var j = 0; j <= sLen; j++) { score[0, j] = GapOpenPenalty + (j - 1) * GapExtendPenalty; trace[0, j] = 2; }

        for (var i = 1; i <= eLen; i++)
        {
            for (var j = 1; j <= sLen; j++)
            {
                var similarity = WordSimilarity(expectedTokens[i - 1], spokenTokens[j - 1]);
                var diagScore = score[i - 1, j - 1] + similarity;
                var upScore = (trace[i - 1, j] == 3 ? GapExtendPenalty : GapOpenPenalty) + score[i - 1, j];
                var leftScore = (trace[i, j - 1] == 2 ? GapExtendPenalty : GapOpenPenalty) + score[i, j - 1];

                if (diagScore >= upScore && diagScore >= leftScore)
                    { score[i, j] = diagScore; trace[i, j] = 1; }
                else if (upScore >= leftScore)
                    { score[i, j] = upScore; trace[i, j] = 3; }
                else
                    { score[i, j] = leftScore; trace[i, j] = 2; }
            }
        }

        var spokenToExpected = new int?[sLen];
        var mismatches = new List<RecitationWordMismatch>();
        var matchedWords = 0;
        var ei = eLen;
        var sj = sLen;

        while (ei > 0 || sj > 0)
        {
            switch (trace[ei, sj])
            {
                case 1:
                    ei--; sj--;
                    spokenToExpected[sj] = ei;
                    if (TokensEqual(expectedTokens[ei], spokenTokens[sj]))
                        matchedWords++;
                    else
                        mismatches.Add(new RecitationWordMismatch(ei, spokenTokens[sj].Original, expectedTokens[ei].Original));
                    break;
                case 2:
                    sj--;
                    spokenToExpected[sj] = null;
                    break;
                case 3:
                    ei--;
                    mismatches.Add(new RecitationWordMismatch(ei, string.Empty, expectedTokens[ei].Original));
                    break;
            }
        }

        mismatches.Reverse();
        var processedWords = matchedWords + mismatches.Count;
        return (matchedWords, processedWords, mismatches, spokenToExpected);
    }

    private static double WordSimilarity(WordToken expected, WordToken spoken)
    {
        if (TokensEqual(expected, spoken))
            return 1.0;

        var ec = RemoveCommonArabicPrefix(expected.Normalized);
        var sc = RemoveCommonArabicPrefix(spoken.Normalized);
        var minLen = Math.Min(ec.Length, sc.Length);
        if (minLen == 0)
            return -0.5;

        return ComputeCharacterSimilarity(ec, sc) - 0.5;
    }

    private static bool TokensEqual(WordToken expected, WordToken spoken)
    {
        if (string.Equals(expected.Normalized, spoken.Normalized, StringComparison.Ordinal))
        {
            return true;
        }

        var expectedCanonical = RemoveCommonArabicPrefix(expected.Normalized);
        var spokenCanonical = RemoveCommonArabicPrefix(spoken.Normalized);
        if (string.Equals(expectedCanonical, spokenCanonical, StringComparison.Ordinal))
        {
            return true;
        }

        var minLength = Math.Min(expectedCanonical.Length, spokenCanonical.Length);
        if (minLength == 0)
        {
            return false;
        }

        var similarity = ComputeCharacterSimilarity(expectedCanonical, spokenCanonical);
        var similarityThreshold = minLength <= 2 ? 0.88 : minLength <= 3 ? 0.72 : 0.60;
        if (similarity >= similarityThreshold)
        {
            return true;
        }

        if (minLength >= 3)
        {
            var lcsRatio = ComputeLongestCommonSubsequenceRatio(expectedCanonical, spokenCanonical);
            var lcsThreshold = minLength <= 4 ? 0.75 : 0.62;
            if (lcsRatio >= lcsThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeConfidence(
        IReadOnlyList<WordToken> expectedTokens,
        IReadOnlyList<WordToken> spokenTokens,
        int matchedWords)
    {
        if (expectedTokens.Count == 0 || spokenTokens.Count == 0)
        {
            return 0;
        }

        var tokenScore = (double)matchedWords / expectedTokens.Count;
        var expectedJoined = string.Join(' ', expectedTokens.Select(t => t.Normalized));
        var spokenJoined = string.Join(' ', spokenTokens.Select(t => t.Normalized));
        var charScore = ComputeCharacterSimilarity(expectedJoined, spokenJoined);
        var coverageScore = (double)Math.Min(spokenTokens.Count, expectedTokens.Count) / expectedTokens.Count;
        var overflowPenalty = spokenTokens.Count <= expectedTokens.Count
            ? 1.0
            : 1.0 / (1.0 + ((double)(spokenTokens.Count - expectedTokens.Count) / expectedTokens.Count));

        var rawScore = (0.55 * tokenScore) + (0.30 * charScore) + (0.15 * coverageScore);
        return Math.Clamp(rawScore * overflowPenalty, 0, 1);
    }

    private static double ComputeCharacterSimilarity(string expected, string spoken)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(spoken))
        {
            return 0;
        }

        var distance = ComputeLevenshteinDistance(expected, spoken);
        var maxLength = Math.Max(expected.Length, spoken.Length);
        if (maxLength == 0)
        {
            return 0;
        }

        return Math.Clamp(1.0 - ((double)distance / maxLength), 0, 1);
    }

    private static string RemoveCommonArabicPrefix(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return token.StartsWith("ال", StringComparison.Ordinal) && token.Length > 2
            ? token[2..]
            : token;
    }

    private static double ComputeLongestCommonSubsequenceRatio(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        var rows = left.Length + 1;
        var cols = right.Length + 1;
        var dp = new int[rows, cols];
        for (var row = 1; row < rows; row++)
        {
            for (var col = 1; col < cols; col++)
            {
                if (left[row - 1] == right[col - 1])
                {
                    dp[row, col] = dp[row - 1, col - 1] + 1;
                }
                else
                {
                    dp[row, col] = Math.Max(dp[row - 1, col], dp[row, col - 1]);
                }
            }
        }

        var lcsLength = dp[rows - 1, cols - 1];
        var minLength = Math.Min(left.Length, right.Length);
        return minLength == 0 ? 0 : (double)lcsLength / minLength;
    }

    private static int ComputeLevenshteinDistance(string source, string target)
    {
        var rows = source.Length + 1;
        var cols = target.Length + 1;
        var distance = new int[rows, cols];

        for (var row = 0; row < rows; row++)
        {
            distance[row, 0] = row;
        }

        for (var col = 0; col < cols; col++)
        {
            distance[0, col] = col;
        }

        for (var row = 1; row < rows; row++)
        {
            for (var col = 1; col < cols; col++)
            {
                var substitutionCost = source[row - 1] == target[col - 1] ? 0 : 1;
                var deletion = distance[row - 1, col] + 1;
                var insertion = distance[row, col - 1] + 1;
                var substitution = distance[row - 1, col - 1] + substitutionCost;
                distance[row, col] = Math.Min(Math.Min(deletion, insertion), substitution);
            }
        }

        return distance[rows - 1, cols - 1];
    }
}
