using System.Text.Json;

namespace TarteelClone.QuranEngine;

/// <summary>
/// Optional source of verified line-level Mushaf data. When a page has line
/// data, the layout engine renders those lines verbatim (authentic 16-liner);
/// otherwise it falls back to the algorithmic distribution.
/// </summary>
public interface ILineLevelPageSource
{
    /// <summary>Returns the 16 line strings for the page, or null when no
    /// verified line data is available for it.</summary>
    IReadOnlyList<string>? TryGetLines(int page);
}

/// <summary>
/// Line-level page data loaded from a JSON array:
/// [{ "page": 1, "lines": ["…", …16 strings…] }, …]
/// Missing/empty data yields null so the engine falls back to its
/// algorithmic layout. Verified data can be dropped in later without code
/// changes — nothing in here claims unverified data is authentic.
/// </summary>
public sealed class JsonLinePageSource : ILineLevelPageSource
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<string>> _pages;

    public JsonLinePageSource()
        : this("[]")
    {
    }

    public JsonLinePageSource(string json)
    {
        _pages = Parse(json);
    }

    public JsonLinePageSource(Stream jsonStream)
    {
        using var reader = new StreamReader(jsonStream);
        _pages = Parse(reader.ReadToEnd());
    }

    public IReadOnlyList<string>? TryGetLines(int page) =>
        _pages.TryGetValue(page, out var lines) && lines is { Count: > 0 } ? lines : null;

    private static IReadOnlyDictionary<int, IReadOnlyList<string>> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var result = new Dictionary<int, IReadOnlyList<string>>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("page", out var pageProp) || pageProp.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                if (!element.TryGetProperty("lines", out var linesProp) || linesProp.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var lines = linesProp.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray();
                if (lines.Length == 0)
                {
                    continue;
                }

                result[pageProp.GetInt32()] = lines;
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
        }
    }
}
