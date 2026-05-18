using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;
using MobileModels = TarteelMobile.Models;

namespace TarteelMobile.Services.Core;

public sealed class VerseRepositoryCoreAdapter : CoreAbstractions.IVerseRepository
{
    private readonly TarteelMobile.Services.IVerseRepository _inner;

    public VerseRepositoryCoreAdapter(TarteelMobile.Services.IVerseRepository inner)
    {
        _inner = inner;
    }

    public async Task<CoreModels.RecitationVerse?> GetVerseAsync(
        int surahNum,
        int ayahNum,
        CancellationToken cancellationToken = default)
    {
        var verse = await _inner.GetVerseAsync(surahNum, ayahNum, cancellationToken);
        return verse is null ? null : MapVerse(verse);
    }

    public async Task<IReadOnlyList<CoreModels.RecitationVerse>> GetMemorizedVersesAsync(
        CancellationToken cancellationToken = default)
    {
        var verses = await _inner.GetMemorizedVersesAsync(cancellationToken: cancellationToken);
        return verses.Select(MapVerse).ToArray();
    }

    private static CoreModels.RecitationVerse MapVerse(MobileModels.Verse verse) =>
        new()
        {
            SurahNum = verse.SurahNum,
            AyahNum = verse.AyahNum,
            ArabicText = verse.ArabicText,
            Translation = verse.Translation
        };
}

public sealed class DiagnosticsProgressStore : CoreAbstractions.IProgressStore
{
    private readonly IVerseRepository _verseRepository;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;

    public DiagnosticsProgressStore(
        IVerseRepository verseRepository,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _verseRepository = verseRepository;
        _session = session;
        _diagnostics = diagnostics;
    }

    public async Task SaveAsync(CoreModels.RecitationMatchResult result, CancellationToken cancellationToken = default)
    {
        _diagnostics.Info(
            $"Progress checkpoint: Surah {result.SurahNum}:{result.AyahNum} confidence {result.Confidence:0.00}.");

        try
        {
            await _verseRepository.RecordRecitationAsync(
                _session.CurrentUserEmail,
                result.SurahNum,
                result.AyahNum,
                result.Confidence,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not persist recitation progress to local store: {ex.Message}");
        }
    }
}
