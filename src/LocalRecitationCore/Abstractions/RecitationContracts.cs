using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Abstractions;

public interface IAudioSource
{
    event EventHandler<byte[]>? AudioChunkReady;
    event EventHandler<Exception>? RecordingError;
    bool IsRecording { get; }
    Task StartRecordingAsync(CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
}

public interface IAsrEngine
{
    bool IsReady { get; }
    string ActiveTier { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<RecitationTranscriptionResult> TranscribeAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default);
}

public interface IVerseMatcher
{
    Task<RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default);
}

public interface IVerseRepository
{
    Task<RecitationVerse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecitationVerse>> GetMemorizedVersesAsync(CancellationToken cancellationToken = default);
}

public interface IProgressStore
{
    Task SaveAsync(RecitationMatchResult result, CancellationToken cancellationToken = default);
}

public interface IRecitationOrchestrator
{
    event EventHandler<RecitationMatchResult>? MatchProduced;
    event EventHandler<string>? DiagnosticEmitted;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubmitAudioChunkAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
}
