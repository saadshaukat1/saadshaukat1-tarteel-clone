using Microsoft.EntityFrameworkCore;
using TarteelClone.UserService.Data;
using TarteelClone.UserService.Models;

namespace TarteelClone.UserService.Services;

public class ProgressService : IProgressService
{
    private readonly UserDbContext _db;

    public ProgressService(UserDbContext db) => _db = db;

    public async Task<IList<MemorizationProgress>> GetProgressAsync(int userId,
        CancellationToken ct = default)
        => await _db.MemorizationProgress
                    .Where(mp => mp.UserId == userId)
                    .OrderBy(mp => mp.SurahNum).ThenBy(mp => mp.AyahNum)
                    .ToListAsync(ct);

    // Exponential moving average to smooth out score updates.
    private const double EmaCurrentWeight = 0.7;
    private const double EmaNewWeight     = 1.0 - EmaCurrentWeight;

    public async Task RecordRecitationAsync(int userId, int surahNum, int ayahNum,
        double masteryScore, CancellationToken ct = default)
    {
        var existing = await _db.MemorizationProgress
            .FirstOrDefaultAsync(mp =>
                mp.UserId == userId &&
                mp.SurahNum == surahNum &&
                mp.AyahNum == ayahNum, ct);

        if (existing is null)
        {
            _db.MemorizationProgress.Add(new MemorizationProgress
            {
                UserId       = userId,
                SurahNum     = surahNum,
                AyahNum      = ayahNum,
                MasteryScore = masteryScore
            });
        }
        else
        {
            existing.MasteryScore = EmaCurrentWeight * existing.MasteryScore + EmaNewWeight * masteryScore;
        }

        await _db.SaveChangesAsync(ct);
    }
}
