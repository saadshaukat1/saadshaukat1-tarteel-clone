namespace TarteelMobile.Services;

/// <summary>
/// Platform-agnostic abstraction over native microphone recording.
/// Each platform (Android, iOS, Windows) provides its own implementation.
/// </summary>
public interface IAudioService
{
    event EventHandler<byte[]>? AudioChunkReady;

    Task StartRecordingAsync();
    Task StopRecordingAsync();
    bool IsRecording { get; }
}

/// <summary>
/// Stub implementation used in unit tests / when no mic is available.
/// Real implementations live in Platforms/ folders.
/// </summary>
public class AudioService : IAudioService
{
    public event EventHandler<byte[]>? AudioChunkReady;
    public bool IsRecording { get; private set; }

    public Task StartRecordingAsync()
    {
        IsRecording = true;
        return Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        IsRecording = false;
        return Task.CompletedTask;
    }

    protected void OnAudioChunkReady(byte[] chunk)
        => AudioChunkReady?.Invoke(this, chunk);
}
