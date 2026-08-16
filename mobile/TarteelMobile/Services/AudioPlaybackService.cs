using Microsoft.Maui.Storage;

namespace TarteelMobile.Services;

public interface IAudioPlaybackService
{
    bool IsPlaying { get; }
    bool HasReferenceAudio(int surahNum, int ayahNum);
    Task PlayVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default);
    Task PlayVerseRangeAsync(int surahNum, int startAyah, int endAyah, CancellationToken cancellationToken = default);
    Task StopAsync();
}

#if WINDOWS
public sealed class AudioPlaybackService : IAudioPlaybackService
{
    private readonly IAppDiagnosticsService _diagnostics;
    private System.Diagnostics.Process? _activeProcess;

    public bool IsPlaying => _activeProcess is { HasExited: false };

    public AudioPlaybackService(IAppDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public bool HasReferenceAudio(int surahNum, int ayahNum)
    {
        var path = GetVerseAudioPath(surahNum, ayahNum);
        return File.Exists(path);
    }

    public async Task PlayVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default)
    {
        var path = GetVerseAudioPath(surahNum, ayahNum);
        if (!File.Exists(path))
        {
            _diagnostics.Warn($"Reference audio not found: {path}");
            return;
        }

        await PlayFileAsync(path, cancellationToken);
    }

    public async Task PlayVerseRangeAsync(int surahNum, int startAyah, int endAyah, CancellationToken cancellationToken = default)
    {
        for (var ayah = startAyah; ayah <= endAyah; ayah++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PlayVerseAsync(surahNum, ayah, cancellationToken);
        }
    }

    public Task StopAsync()
    {
        if (_activeProcess is { HasExited: false })
        {
            try
            {
                _activeProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _diagnostics.Warn($"Could not stop audio playback: {ex.Message}");
            }
            finally
            {
                _activeProcess.Dispose();
                _activeProcess = null;
            }
        }

        return Task.CompletedTask;
    }

    private async Task PlayFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await StopAsync();

        var processInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        };

        _activeProcess = System.Diagnostics.Process.Start(processInfo);
        if (_activeProcess is null)
        {
            _diagnostics.Warn($"Could not start audio playback for {filePath}");
            return;
        }

        try
        {
            await _activeProcess.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await StopAsync();
        }
        finally
        {
            _activeProcess?.Dispose();
            _activeProcess = null;
        }
    }

    private static string GetVerseAudioPath(int surahNum, int ayahNum)
    {
        var audioDir = Path.Combine(
            FileSystem.AppDataDirectory,
            "reference_audio",
            surahNum.ToString("000"));
        return Path.Combine(audioDir, $"{ayahNum:000}.wav");
    }
}
#endif
