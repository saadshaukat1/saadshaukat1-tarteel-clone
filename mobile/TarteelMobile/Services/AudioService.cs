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
    // 2.5 s chunks allow live, word-by-word progressive recitation reveal
    // while keeping inference reliable and avoiding speech cutoff delays.
    public const int ChunkDurationMilliseconds = 2500;
    public const int ChunkOverlapMilliseconds = 500;
    public const int CaptureBufferMilliseconds = 100;
}
