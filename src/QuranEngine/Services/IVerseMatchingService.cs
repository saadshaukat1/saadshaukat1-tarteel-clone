using TarteelClone.QuranEngine.Models;

namespace TarteelClone.QuranEngine.Services;

public interface IVerseMatchingService
{
    Task<Verse?>      GetVerseAsync(int surahNum, int ayahNum, CancellationToken ct = default);
    Task<IList<Verse>> GetSurahAsync(int surahNum, CancellationToken ct = default);
    Task<MatchResult>  MatchAsync(string arabicText, CancellationToken ct = default);
}
