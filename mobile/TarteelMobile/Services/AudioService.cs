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
    // Lower chunk duration improves perceived recitation responsiveness.
    public const int ChunkDurationMilliseconds = 2200;
    public const int CaptureBufferMilliseconds = 100;
}
