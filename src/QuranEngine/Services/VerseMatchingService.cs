using FuzzySharp;
using Microsoft.EntityFrameworkCore;
using TarteelClone.QuranEngine.Data;
using TarteelClone.QuranEngine.Models;

namespace TarteelClone.QuranEngine.Services;

/// <summary>
/// Matches transcribed Arabic text against stored Quran verses using
/// FuzzySharp (Levenshtein / token-set ratio) for tolerance of tajweed
/// variations and minor transcription errors.
/// </summary>
public class VerseMatchingService : IVerseMatchingService
{
    private readonly QuranDbContext _db;

    public VerseMatchingService(QuranDbContext db) => _db = db;

    public Task<Verse?> GetVerseAsync(int surahNum, int ayahNum,
        CancellationToken ct = default)
        => _db.Verses
              .Include(v => v.Translations)
              .FirstOrDefaultAsync(v => v.SurahNum == surahNum && v.AyahNum == ayahNum, ct);

    public async Task<IList<Verse>> GetSurahAsync(int surahNum,
        CancellationToken ct = default)
        => await _db.Verses
                    .Where(v => v.SurahNum == surahNum)
                    .OrderBy(v => v.AyahNum)
                    .ToListAsync(ct);

    /// <summary>
    /// Finds the best-matching verse for the given transcribed Arabic text,
    /// then computes per-word mismatches.
    /// </summary>
    public async Task<MatchResult> MatchAsync(string arabicText,
        CancellationToken ct = default)
    {
        // Load all verses (production: cache this, don't hit DB per call).
        var verses = await _db.Verses.ToListAsync(ct);

        Verse? bestVerse = null;
        int    bestScore = -1;

        foreach (var verse in verses)
        {
            int score = Fuzz.TokenSetRatio(arabicText, verse.ArabicText);
            if (score > bestScore)
            {
                bestScore = score;
                bestVerse = verse;
            }
        }

        if (bestVerse is null)
            return new MatchResult { Confidence = 0 };

        double confidence = bestScore / 100.0;
        var mismatches    = ComputeMismatches(arabicText, bestVerse.ArabicText);

        return new MatchResult
        {
            SurahNum   = bestVerse.SurahNum,
            AyahNum    = bestVerse.AyahNum,
            ArabicText = bestVerse.ArabicText,
            Confidence = confidence,
            Mismatches = mismatches
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<WordMismatch> ComputeMismatches(string spoken, string expected)
    {
        var spokenWords   = spoken.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expectedWords = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mismatches    = new List<WordMismatch>();

        int len = Math.Min(spokenWords.Length, expectedWords.Length);
        for (int i = 0; i < len; i++)
        {
            if (Fuzz.Ratio(spokenWords[i], expectedWords[i]) < 80)
                mismatches.Add(new WordMismatch(i, spokenWords[i], expectedWords[i]));
        }

        // Any extra expected words are also mismatches (missing words).
        for (int i = len; i < expectedWords.Length; i++)
            mismatches.Add(new WordMismatch(i, string.Empty, expectedWords[i]));

        return mismatches;
    }
}
