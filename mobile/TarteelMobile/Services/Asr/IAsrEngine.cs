using TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Asr;

/// <summary>
/// Abstraction for local ASR engines used by desktop/offline recitation.
/// </summary>
public interface IAsrEngine
{
    bool IsReady { get; }
    string ActiveTier { get; }
    bool IsUsingMockMode { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<RecitationTranscriptionResult> TranscribeAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
}
