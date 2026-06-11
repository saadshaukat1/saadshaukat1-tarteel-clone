using TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Asr;

/// <summary>
/// Abstraction for local ASR engines used by desktop/offline recitation.
/// </summary>
public interface IAsrEngine
{
    bool IsReady { get; }

    /// <summary>Returns true when the model file for the configured primary tier already exists on disk.</summary>
    bool IsModelPresent { get; }

    string ActiveTier { get; }
    bool IsUsingMockMode { get; }

    /// <summary>Fired during model download or file import. Value is 0.0–1.0, or -1 when size is unknown.</summary>
    event EventHandler<AsrDownloadProgress>? DownloadProgressChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a locally-selected model file to the persistent model path and then initializes the engine.
    /// Use this when the user picks a .bin/.gguf file from disk instead of downloading.
    /// </summary>
    Task ImportModelFromFileAsync(string sourcePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a model from an already-opened stream to the persistent model path and then initializes the engine.
    /// Prefer this over <see cref="ImportModelFromFileAsync"/> on Windows MAUI where the FilePicker
    /// holds a file lock that prevents <see cref="File.OpenRead"/> from succeeding.
    /// </summary>
    Task ImportModelFromStreamAsync(Stream sourceStream, long? totalBytes, CancellationToken cancellationToken = default);

    Task<RecitationTranscriptionResult> TranscribeAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
    void SetSurahPrompt(string surahArabicName);
    void ClearSurahPrompt();
}

public sealed record AsrDownloadProgress(
    string Tier,
    double Fraction,       // 0.0 � 1.0, or -1 when total size unknown
    long BytesReceived,
    long? TotalBytes,
    string StatusMessage);

