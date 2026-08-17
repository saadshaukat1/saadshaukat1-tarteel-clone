namespace TarteelMobile.Services;

public sealed record ReadinessCheckResult(
    string Name,
    bool IsReady,
    string Details,
    bool IsRequired = true);

public sealed record OfflineReadinessReport(
    bool IsReady,
    IReadOnlyList<ReadinessCheckResult> Checks)
{
    public string Summary =>
        IsReady
            ? "Offline readiness checks passed."
            : $"Offline readiness checks failed: {Checks.Count(c => c.IsRequired && !c.IsReady)} issue(s).";
}

public interface IOfflineReadinessService
{
    Task<OfflineReadinessReport> RunStartupChecksAsync();
}

public sealed class OfflineReadinessService : IOfflineReadinessService
{
    private readonly IAudioService _audio;
    private readonly IVerseRepository _verses;
    private readonly TarteelMobile.Services.Asr.IAsrEngine _asrEngine;
    private readonly IAppDiagnosticsService _diagnostics;

    public OfflineReadinessService(
        IAudioService audio,
        IVerseRepository verses,
        TarteelMobile.Services.Asr.IAsrEngine asrEngine,
        IAppDiagnosticsService diagnostics)
    {
        _audio = audio;
        _verses = verses;
        _asrEngine = asrEngine;
        _diagnostics = diagnostics;
    }

    public async Task<OfflineReadinessReport> RunStartupChecksAsync()
    {
        var checks = new List<ReadinessCheckResult>();

        await _verses.EnsureInitializedAsync();
        var verseCount = await _verses.GetVerseCountAsync();
        checks.Add(new ReadinessCheckResult(
            Name: "Local SQLite Quran store",
            IsReady: verseCount > 0,
            Details: $"DB has {verseCount} verse record(s)."));

        var verse = await _verses.GetVerseAsync(1, 1);
        checks.Add(new ReadinessCheckResult(
            Name: "Local verse repository",
            IsReady: verse is not null,
            Details: verse is null
                ? "No local verse available."
                : $"Sample verse loaded: {verse.SurahNum}:{verse.AyahNum}."));

        checks.Add(new ReadinessCheckResult(
            Name: "Audio service",
            IsReady: true,
            Details: _audio.IsRecording
                ? "Audio service initialized and currently recording."
                : "Audio service initialized (idle)."));

        checks.Add(new ReadinessCheckResult(
            Name: "Diagnostics sink",
            IsReady: !string.IsNullOrWhiteSpace(_diagnostics.LogPath),
            Details: $"Log file path: {_diagnostics.LogPath}"));

        // Wrap ASR init in try/catch — libggml-whisper.so can abort() natively
        // if the model file is missing/corrupt, which bypasses C# exception handling.
        // The TryBuildFactory guard handles that case now, but we still catch here
        // as a belt-and-suspenders measure. ASR is non-required so a failure here
        // must never prevent the app from starting.
        string asrDetails;
        bool asrReady = false;
        try
        {
            await _asrEngine.InitializeAsync();
            asrReady = _asrEngine.IsReady && !_asrEngine.IsUsingMockMode;
            asrDetails = _asrEngine.IsReady
                ? (_asrEngine.IsUsingMockMode
                    ? $"ASR running in mock mode on tier '{_asrEngine.ActiveTier}' (real assets not found)."
                    : $"ASR ready on tier '{_asrEngine.ActiveTier}'.")
                : "ASR assets not installed; recitation will be unavailable until assets are placed.";
        }
        catch (Exception ex)
        {
            asrReady = false;
            asrDetails = $"ASR initialization failed: {ex.Message}";
            _diagnostics.Warn($"ASR init threw during startup check: {ex}");
        }

        checks.Add(new ReadinessCheckResult(
            Name: "Local ASR runtime",
            IsReady: asrReady,
            Details: asrDetails,
            IsRequired: false));

        var report = new OfflineReadinessReport(
            IsReady: checks.Where(c => c.IsRequired).All(c => c.IsReady),
            Checks: checks);

        if (report.IsReady)
        {
            _diagnostics.Info(report.Summary);
        }
        else
        {
            _diagnostics.Warn(report.Summary);
        }

        foreach (var check in checks)
        {
            var levelPrefix = check.IsReady ? "Check passed" : "Check failed";
            _diagnostics.Info($"{levelPrefix}: {check.Name} - {check.Details}");
        }

        return report;
    }
}
