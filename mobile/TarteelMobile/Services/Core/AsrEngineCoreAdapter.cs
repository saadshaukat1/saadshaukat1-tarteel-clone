using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;
using System.Diagnostics;

namespace TarteelMobile.Services.Core;

/// <summary>
/// Bridges MAUI ASR service to shared local recitation core abstraction.
/// </summary>
public sealed class AsrEngineCoreAdapter : CoreAbstractions.IAsrEngine
{
    private readonly TarteelMobile.Services.Asr.IAsrEngine _inner;
    private readonly IAppDiagnosticsService _diagnostics;
    private long _attemptCount;
    private long _failureCount;
    private long _emptyCount;
    private long _successCount;

    public AsrEngineCoreAdapter(
        TarteelMobile.Services.Asr.IAsrEngine inner,
        IAppDiagnosticsService diagnostics)
    {
        _inner = inner;
        _diagnostics = diagnostics;
    }

    public bool IsReady => _inner.IsReady;
    public string ActiveTier => _inner.ActiveTier;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _inner.InitializeAsync(cancellationToken);

    public async Task<CoreModels.RecitationTranscriptionResult> TranscribeAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default)
    {
        var attempt = Interlocked.Increment(ref _attemptCount);
        var stopwatch = Stopwatch.StartNew();
        var result = await _inner.TranscribeAsync(audioChunk, cancellationToken);
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        if (!result.IsSuccess)
        {
            var failure = Interlocked.Increment(ref _failureCount);
            if (failure <= 5 || failure % 10 == 0)
            {
                _diagnostics.Warn(
                    $"ASR attempt #{attempt} failed. Tier={result.TierUsed} Fallback={result.UsedFallback} " +
                    $"DurationMs={elapsedMs} Reason='{result.DiagnosticMessage ?? "n/a"}'.");
            }

            return result;
        }

        if (string.IsNullOrWhiteSpace(result.Text))
        {
            var empty = Interlocked.Increment(ref _emptyCount);
            if (empty <= 5 || empty % 10 == 0)
            {
                _diagnostics.Warn(
                    $"ASR attempt #{attempt} returned empty transcript. Tier={result.TierUsed} " +
                    $"DurationMs={elapsedMs} MockMode={_inner.IsUsingMockMode} Reason='{result.DiagnosticMessage ?? "n/a"}'.");
            }

            return result;
        }

        var success = Interlocked.Increment(ref _successCount);
        if (success <= 5 || success % 10 == 0)
        {
            var preview = result.Text.Length <= 80 ? result.Text : $"{result.Text[..80]}…";
            _diagnostics.Info(
                $"ASR attempt #{attempt} recognized text (len={result.Text.Length}, conf={result.Confidence:0.00}, tier={result.TierUsed}, durationMs={elapsedMs}). " +
                $"Preview: '{preview}'.");
        }

        return result;
    }
}
