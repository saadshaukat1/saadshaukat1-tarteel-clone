using TarteelClone.UserService.Models;

namespace TarteelClone.UserService.Services;

public interface IProgressService
{
    Task<IList<MemorizationProgress>> GetProgressAsync(int userId, CancellationToken ct = default);
    Task RecordRecitationAsync(int userId, int surahNum, int ayahNum,
        double masteryScore, CancellationToken ct = default);
}
