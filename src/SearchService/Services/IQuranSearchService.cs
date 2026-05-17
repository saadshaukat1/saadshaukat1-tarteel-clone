using TarteelClone.SearchService.Models;

namespace TarteelClone.SearchService.Services;

public interface IQuranSearchService
{
    Task<IList<VerseSearchResult>> SearchAsync(string query, string language = "en",
        CancellationToken ct = default);
}
