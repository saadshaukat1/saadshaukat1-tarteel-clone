#if !WINDOWS && !ANDROID
namespace TarteelMobile.Services;

/// <summary>
/// Non-Windows fallback while Windows capture is implemented first.
/// </summary>
public class AudioService : IAudioService
{
    public event EventHandler<byte[]>? AudioChunkReady;
    public event EventHandler<Exception>? RecordingError;
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
}
#endif
