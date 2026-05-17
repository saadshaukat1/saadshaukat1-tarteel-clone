using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Core;

/// <summary>
/// Bridges MAUI ASR service to shared local recitation core abstraction.
/// </summary>
public sealed class AsrEngineCoreAdapter : CoreAbstractions.IAsrEngine
{
    private readonly TarteelMobile.Services.Asr.IAsrEngine _inner;

    public AsrEngineCoreAdapter(TarteelMobile.Services.Asr.IAsrEngine inner)
    {
        _inner = inner;
    }

    public bool IsReady => _inner.IsReady;
    public string ActiveTier => _inner.ActiveTier;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _inner.InitializeAsync(cancellationToken);

    public Task<CoreModels.RecitationTranscriptionResult> TranscribeAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default) =>
        _inner.TranscribeAsync(audioChunk, cancellationToken);
}
