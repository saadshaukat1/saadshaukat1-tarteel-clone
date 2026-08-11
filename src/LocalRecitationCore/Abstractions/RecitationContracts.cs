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
    void SetSurahPrompt(string surahArabicName);
    void ClearSurahPrompt();
}

public interface IVerseMatcher
{
    void SetSurahContext(int surahNumber);
    void ClearSurahContext();
    void SetLastMatchedPosition(int surahNum, int ayahNum);
    void ClearLastMatchedPosition();
    void MarkCurrentAyahComplete();
    Task<RecitationMatchResult> MatchAsync(
        string arabicText,
        CancellationToken cancellationToken = default);
}

public interface IVerseRepository
{
    Task<RecitationVerse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecitationVerse>> GetMemorizedVersesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecitationVerse>> GetAllVersesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecitationVerse>> GetCandidateVersesAsync(IReadOnlyList<string> normalizedWords, CancellationToken cancellationToken = default);
}

public interface IProgressStore
{
    Task SaveAsync(RecitationMatchResult result, CancellationToken cancellationToken = default);
}

public interface IRecitationOrchestrator
{
    event EventHandler<RecitationMatchResult>? MatchProduced;
    event EventHandler<string>? DiagnosticEmitted;
    void SetSurahContext(int surahNum);
    void ClearSurahContext();
    void ClearLastMatchedPosition();
    void MarkCurrentAyahComplete();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubmitAudioChunkAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
}
