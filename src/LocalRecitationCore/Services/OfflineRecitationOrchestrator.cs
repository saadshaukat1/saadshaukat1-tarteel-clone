using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Minimal local recitation pipeline for offline desktop scenarios.
/// It is intentionally simple so parallel audio/asr/sqlite phases can extend it.
/// </summary>
public sealed class OfflineRecitationOrchestrator : IRecitationOrchestrator
{
    private readonly IAsrEngine _asrEngine;
    private readonly IVerseMatcher _verseMatcher;
    private readonly IProgressStore _progressStore;
    private readonly object _sync = new();
    private bool _isRunning;

    public OfflineRecitationOrchestrator(
        IAsrEngine asrEngine,
        IVerseMatcher verseMatcher,
        IProgressStore progressStore)
    {
        _asrEngine = asrEngine;
        _verseMatcher = verseMatcher;
        _progressStore = progressStore;
    }

    public event EventHandler<RecitationMatchResult>? MatchProduced;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isRunning = true;
        }

        await _asrEngine.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isRunning = false;
        }

        return Task.CompletedTask;
    }

    public async Task SubmitAudioChunkAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default)
    {
        if (audioChunk is null || audioChunk.Length == 0)
        {
            return;
        }

        bool isRunning;
        lock (_sync)
        {
            isRunning = _isRunning;
        }

        if (!isRunning)
        {
            return;
        }

        var transcription = await _asrEngine.TranscribeAsync(audioChunk, cancellationToken);
        if (!transcription.IsSuccess)
        {
            return;
        }

        var match = await _verseMatcher.MatchAsync(transcription.Text, cancellationToken);
        await _progressStore.SaveAsync(match, cancellationToken);
        MatchProduced?.Invoke(this, match);
    }
}
