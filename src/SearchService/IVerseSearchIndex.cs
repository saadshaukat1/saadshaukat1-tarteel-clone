using System.Text.RegularExpressions;

namespace TarteelClone.SearchService;

public sealed record VerseSearchResult(int SurahNum, int AyahNum, string ArabicText, double Score);

public interface IVerseSearchIndex
{
    IReadOnlyList<VerseSearchResult> Search(IReadOnlyList<string> queryTokens, int maxResults = 20);
    void Index(IReadOnlyList<(int SurahNum, int AyahNum, string ArabicText)> verses);
}

public sealed class WordMatchSearchIndex : IVerseSearchIndex
{
    private readonly Dictionary<string, List<(int Surah, int Ayah, string Text)>> _index = new(StringComparer.Ordinal);
    private readonly Dictionary<(int, int), string> _fullText = new();

    private static readonly Regex TokenSplitter = new(@"\w+", RegexOptions.Compiled);

    public void Index(IReadOnlyList<(int SurahNum, int AyahNum, string ArabicText)> verses)
    {
        _index.Clear();
        _fullText.Clear();
        foreach (var (surah, ayah, text) in verses)
        {
            _fullText[(surah, ayah)] = text;
            var tokens = Tokenize(text);
            foreach (var token in tokens)
            {
                if (!_index.TryGetValue(token, out var list))
                {
                    list = [];
                    _index[token] = list;
                }
                list.Add((surah, ayah, text));
            }
        }
    }

    public IReadOnlyList<VerseSearchResult> Search(IReadOnlyList<string> queryTokens, int maxResults = 20)
    {
        if (queryTokens.Count == 0)
        {
            return [];
        }

        var normalizedTokens = queryTokens
            .Select(t => Normalize(t))
            .Where(t => t.Length > 0)
            .ToArray();

        if (normalizedTokens.Length == 0)
        {
            return [];
        }

        var scored = new Dictionary<(int, int), VerseSearchResult>();
        foreach (var token in normalizedTokens)
        {
            if (!_index.TryGetValue(token, out var matches))
            {
                continue;
            }

            foreach (var (surah, ayah, text) in matches)
            {
                var key = (surah, ayah);
                if (scored.TryGetValue(key, out var existing))
                {
                    scored[key] = existing with { Score = existing.Score + 1.0 / normalizedTokens.Length };
                }
                else
                {
                    scored[key] = new VerseSearchResult(surah, ayah, text, 1.0 / normalizedTokens.Length);
                }
            }
        }

        return scored.Values
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.SurahNum)
            .ThenBy(r => r.AyahNum)
            .Take(maxResults)
            .ToArray();
    }

    private static string[] Tokenize(string arabicText)
    {
        return arabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();
    }

    private static string Normalize(string word) => TokenSplitter.Replace(word.Trim(), "").ToLowerInvariant();
}
