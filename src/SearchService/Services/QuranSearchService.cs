using Nest;
using TarteelClone.SearchService.Models;

namespace TarteelClone.SearchService.Services;

/// <summary>
/// Searches the Quran index in Elasticsearch using multi-field queries
/// (Arabic text + translated text).
/// </summary>
public class QuranSearchService : IQuranSearchService
{
    private readonly IElasticClient _elastic;
    private const    string         IndexName = "quran_verses";

    public QuranSearchService(IElasticClient elastic) => _elastic = elastic;

    public async Task<IList<VerseSearchResult>> SearchAsync(string query,
        string language = "en", CancellationToken ct = default)
    {
        var response = await _elastic.SearchAsync<VerseDocument>(s => s
            .Index(IndexName)
            .Query(q => q
                .MultiMatch(mm => mm
                    .Fields(f => f
                        .Field(d => d.ArabicText, boost: 2)
                        .Field(d => d.Translation))
                    .Query(query)
                    .Fuzziness(Fuzziness.Auto)))
            .Size(20), ct);

        if (!response.IsValid)
            return [];

        return response.Hits
            .Select(h => new VerseSearchResult(
                h.Source.SurahNum,
                h.Source.AyahNum,
                h.Source.ArabicText,
                h.Source.Translation,
                h.Score ?? 0))
            .ToList();
    }

    // ── Internal Elasticsearch document model ─────────────────────────────────

    private class VerseDocument
    {
        public int    SurahNum    { get; set; }
        public int    AyahNum     { get; set; }
        public string ArabicText  { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }
}
