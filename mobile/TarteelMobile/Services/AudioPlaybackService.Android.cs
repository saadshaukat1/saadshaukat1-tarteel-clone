#if ANDROID
using Android.Media;
using Microsoft.Maui.Storage;
using Uri = Android.Net.Uri;

namespace TarteelMobile.Services;

public sealed class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly IAppDiagnosticsService _diagnostics;
    private MediaPlayer? _player;
    private TaskCompletionSource<bool>? _tcs;

    public bool IsPlaying => _player?.IsPlaying == true;

    public AudioPlaybackService(IAppDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public bool HasReferenceAudio(int surahNum, int ayahNum)
    {
        return File.Exists(GetVerseAudioPath(surahNum, ayahNum));
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
        try
        {
            if (_player?.IsPlaying == true)
                _player.Stop();

            _tcs?.TrySetResult(false);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not stop audio playback: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private async Task PlayFileAsync(string filePath, CancellationToken cancellationToken)
    {
        await StopAsync();
        DisposePlayer();

        _tcs = new TaskCompletionSource<bool>();

        _player = new MediaPlayer();
        _player.SetDataSource(filePath);
        _player.Prepare();

        _player.Completion += (_, _) => _tcs.TrySetResult(true);
        _player.Error += (_, args) =>
        {
            _diagnostics.Warn($"MediaPlayer error: {args.What}");
            _tcs.TrySetResult(false);
        };

        _player.Start();

        using var reg = cancellationToken.Register(() =>
        {
            StopAsync().GetAwaiter().GetResult();
        });

        await _tcs.Task;
    }

    private static string GetVerseAudioPath(int surahNum, int ayahNum)
    {
        var audioDir = Path.Combine(
            FileSystem.AppDataDirectory,
            "reference_audio",
            surahNum.ToString("000"));
        return Path.Combine(audioDir, $"{ayahNum:000}.wav");
    }

    private void DisposePlayer()
    {
        _player?.Reset();
        _player?.Dispose();
        _player = null;
    }

    public void Dispose() => DisposePlayer();
}
#endif
