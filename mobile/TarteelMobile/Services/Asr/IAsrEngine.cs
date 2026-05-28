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

    /// <summary>Fired during model download. Value is 0.0–1.0, or -1 when size is unknown.</summary>
    event EventHandler<AsrDownloadProgress>? DownloadProgressChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<RecitationTranscriptionResult> TranscribeAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
}

public sealed record AsrDownloadProgress(
    string Tier,
    double Fraction,       // 0.0 – 1.0, or -1 when total size unknown
    long BytesReceived,
    long? TotalBytes,
    string StatusMessage);

