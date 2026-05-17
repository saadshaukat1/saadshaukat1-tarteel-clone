using System.Globalization;
using System.Text;
using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Core;

/// <summary>
/// Baseline matcher for local desktop flow until QuranEngine extraction lands.
/// </summary>
public sealed class PlaceholderVerseMatcher : CoreAbstractions.IVerseMatcher
{
    private readonly CoreAbstractions.IVerseRepository _verseRepository;
    private sealed record WordToken(string Original, string Normalized);

    public PlaceholderVerseMatcher(CoreAbstractions.IVerseRepository verseRepository)
    {
        _verseRepository = verseRepository;
    }

    public async Task<CoreModels.RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default)
    {
        var verse = await _verseRepository.GetVerseAsync(1, 1, cancellationToken);
        if (verse is null)
        {
            return new CoreModels.RecitationMatchResult { Confidence = 0 };
        }

        var expectedText = verse.ArabicText;
        var expectedTokens = Tokenize(expectedText);
        var spokenTokens = Tokenize(arabicText);
        if (spokenTokens.Count == 0 || expectedTokens.Count == 0)
        {
            return new CoreModels.RecitationMatchResult
            {
                SurahNum = verse.SurahNum,
                AyahNum = verse.AyahNum,
                ArabicText = string.Empty,
                Confidence = 0,
                Mismatches = []
            };
        }

        var (matchedWords, mismatches) = CompareTokens(expectedTokens, spokenTokens);
        var confidence = ComputeConfidence(expectedTokens, spokenTokens, matchedWords);

        return new CoreModels.RecitationMatchResult
        {
            SurahNum = verse.SurahNum,
            AyahNum = verse.AyahNum,
            ArabicText = expectedText,
            Confidence = confidence,
            ProcessedWordCount = Math.Min(spokenTokens.Count, expectedTokens.Count),
            MatchedWordCount = matchedWords,
            Mismatches = mismatches
        };
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
            var normalized = NormalizeForComparison(part);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            tokens.Add(new WordToken(part, normalized));
        }

        return tokens;
    }

    private static (int MatchedWords, IReadOnlyList<CoreModels.RecitationWordMismatch> Mismatches) CompareTokens(
        IReadOnlyList<WordToken> expectedTokens,
        IReadOnlyList<WordToken> spokenTokens)
    {
        var mismatches = new List<CoreModels.RecitationWordMismatch>();
        var matchedWords = 0;
        var expectedIndex = 0;
        var spokenIndex = 0;

        while (expectedIndex < expectedTokens.Count && spokenIndex < spokenTokens.Count)
        {
            if (TokensEqual(expectedTokens[expectedIndex], spokenTokens[spokenIndex]))
            {
                matchedWords++;
                expectedIndex++;
                spokenIndex++;
                continue;
            }

            var spokenLookaheadMatches = spokenIndex + 1 < spokenTokens.Count &&
                TokensEqual(expectedTokens[expectedIndex], spokenTokens[spokenIndex + 1]);
            var expectedLookaheadMatches = expectedIndex + 1 < expectedTokens.Count &&
                TokensEqual(expectedTokens[expectedIndex + 1], spokenTokens[spokenIndex]);

            if (spokenLookaheadMatches && !expectedLookaheadMatches)
            {
                spokenIndex++;
                continue;
            }

            if (expectedLookaheadMatches && !spokenLookaheadMatches)
            {
                mismatches.Add(new CoreModels.RecitationWordMismatch(
                    expectedIndex,
                    string.Empty,
                    expectedTokens[expectedIndex].Original));
                expectedIndex++;
                continue;
            }

            mismatches.Add(new CoreModels.RecitationWordMismatch(
                expectedIndex,
                spokenTokens[spokenIndex].Original,
                expectedTokens[expectedIndex].Original));
            expectedIndex++;
            spokenIndex++;
        }

        while (expectedIndex < expectedTokens.Count)
        {
            mismatches.Add(new CoreModels.RecitationWordMismatch(
                expectedIndex,
                string.Empty,
                expectedTokens[expectedIndex].Original));
            expectedIndex++;
        }

        return (matchedWords, mismatches);
    }

    private static bool TokensEqual(WordToken expected, WordToken spoken) =>
        string.Equals(expected.Normalized, spoken.Normalized, StringComparison.Ordinal);

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

    private static string NormalizeForComparison(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mappedCharacter = character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ؤ' => 'و',
                'ئ' or 'ى' => 'ي',
                'ة' => 'ه',
                'ـ' => '\0',
                _ => character
            };

            if (mappedCharacter == '\0')
            {
                continue;
            }

            if (char.IsLetterOrDigit(mappedCharacter) || char.IsWhiteSpace(mappedCharacter))
            {
                builder.Append(mappedCharacter);
            }
        }

        var compact = builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        if (compact.Length == 0)
        {
            return string.Empty;
        }

        var normalized = compact.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', normalized);
    }
}
