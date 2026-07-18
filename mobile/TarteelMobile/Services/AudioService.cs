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
    // 8 s chunks strike a balance for CPU-bound Whisper: fewer inference calls per
    // minute than 5 s chunks (roughly half), while staying well within Whisper's 30 s
    // context window for accurate Arabic transcription on weak hardware.
    public const int ChunkDurationMilliseconds = 8000;
    // Arabic recitation has elongated vowels (madd), so a small overlap reduces
    // word-boundary cuts without wasting too much compute. 1 s of overlap between
    // 8 s chunks (~12%) is enough for madd continuity at low CPU cost.
    public const int ChunkOverlapMilliseconds = 1000;
    public const int CaptureBufferMilliseconds = 100;
}
