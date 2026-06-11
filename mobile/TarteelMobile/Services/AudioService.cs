namespace TarteelMobile.Services;

/// <summary>
/// Platform-agnostic abstraction over native microphone recording.
/// </summary>
public interface IAudioService
{
    event EventHandler<byte[]>? AudioChunkReady;
    event EventHandler<Exception>? RecordingError;

    Task StartRecordingAsync();
    Task StopRecordingAsync();
    bool IsRecording { get; }
}

/// <summary>
/// Shared defaults for PCM/WAV chunk capture used by platform services.
/// </summary>
public static class AudioCaptureDefaults
{
    public const int SampleRateHz = 16000;
    public const short BitsPerSample = 16;
    public const short Channels = 1;
    // 5 s chunks give Whisper enough audio for accurate Arabic transcription.
    // Shorter chunks (< 3 s) cause consistent timeouts on ggml-small running on CPU.
    public const int ChunkDurationMilliseconds = 5000;
    // Arabic recitation has elongated vowels (madd), so a wider overlap reduces
    // word-boundary cuts. 1.5 s of overlap between 5 s chunks (~30%) improves accuracy
    // for madd-heavy recitations at modest CPU cost.
    public const int ChunkOverlapMilliseconds = 1500;
    public const int CaptureBufferMilliseconds = 100;
}
