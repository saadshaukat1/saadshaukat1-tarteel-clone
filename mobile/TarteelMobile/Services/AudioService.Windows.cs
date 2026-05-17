#if WINDOWS
using NAudio.Wave;

namespace TarteelMobile.Services;

/// <summary>
/// Windows microphone capture service that emits WAV chunks suitable for ASR.
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly object _sync = new();
    private WaveInEvent? _waveIn;
    private MemoryStream? _currentChunkStream;
    private bool _disposed;
    private DateTimeOffset _chunkStartedAtUtc;
    private Exception? _captureError;

    public event EventHandler<byte[]>? AudioChunkReady;
    public event EventHandler<Exception>? RecordingError;
    public bool IsRecording { get; private set; }

    public Task StartRecordingAsync()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (IsRecording)
            {
                return Task.CompletedTask;
            }

            _captureError = null;
            _currentChunkStream = new MemoryStream();
            _chunkStartedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(
                        AudioCaptureDefaults.SampleRateHz,
                        AudioCaptureDefaults.BitsPerSample,
                        AudioCaptureDefaults.Channels),
                    BufferMilliseconds = AudioCaptureDefaults.CaptureBufferMilliseconds
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();
                IsRecording = true;
            }
            catch (Exception ex)
            {
                CleanupCaptureUnsafe();
                _captureError = ex;
                throw new InvalidOperationException("Unable to start microphone capture.", ex);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        byte[]? finalChunkToEmit = null;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!IsRecording && _waveIn is null)
            {
                return Task.CompletedTask;
            }

            if (_waveIn is not null)
            {
                try
                {
                    _waveIn.StopRecording();
                }
                catch (Exception ex)
                {
                    CleanupCaptureUnsafe();
                    IsRecording = false;
                    throw new InvalidOperationException("Failed to stop microphone capture cleanly.", ex);
                }
            }

            // Flush any pending buffered data as final chunk.
            finalChunkToEmit = BuildWavChunkAndResetUnsafe();
            _currentChunkStream?.Dispose();
            _currentChunkStream = null;
            IsRecording = false;
        }

        if (finalChunkToEmit is { Length: > 0 })
        {
            AudioChunkReady?.Invoke(this, finalChunkToEmit);
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        byte[]? chunkToEmit = null;

        lock (_sync)
        {
            if (!IsRecording || _currentChunkStream is null)
            {
                return;
            }

            _currentChunkStream.Write(e.Buffer, 0, e.BytesRecorded);

            var elapsed = DateTimeOffset.UtcNow - _chunkStartedAtUtc;
            if (elapsed.TotalMilliseconds >= AudioCaptureDefaults.ChunkDurationMilliseconds)
            {
                chunkToEmit = BuildWavChunkAndResetUnsafe();
            }
        }

        if (chunkToEmit is { Length: > 0 })
        {
            AudioChunkReady?.Invoke(this, chunkToEmit);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        Exception? errorToReport = null;

        lock (_sync)
        {
            // If recording stopped unexpectedly while running, keep details.
            if (e.Exception is not null && IsRecording)
            {
                _captureError = e.Exception;
            }

            CleanupWaveInUnsafe();
            _currentChunkStream?.Dispose();
            _currentChunkStream = null;
            IsRecording = false;

            if (_captureError is not null)
            {
                errorToReport = _captureError;
                _captureError = null;
            }
        }

        if (errorToReport is not null)
        {
            RecordingError?.Invoke(this, new InvalidOperationException(
                "Microphone capture stopped due to an error.",
                errorToReport));
        }
    }

    private byte[] BuildWavChunkAndResetUnsafe()
    {
        if (_currentChunkStream is null || _currentChunkStream.Length == 0)
        {
            _currentChunkStream?.Dispose();
            _chunkStartedAtUtc = DateTimeOffset.UtcNow;
            _currentChunkStream = new MemoryStream();
            return [];
        }

        byte[] pcmBytes = _currentChunkStream.ToArray();
        _currentChunkStream.Dispose();
        _currentChunkStream = new MemoryStream();
        _chunkStartedAtUtc = DateTimeOffset.UtcNow;

        return BuildWaveFileBytes(pcmBytes);
    }

    private static byte[] BuildWaveFileBytes(byte[] pcmBytes)
    {
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, new WaveFormat(
                   AudioCaptureDefaults.SampleRateHz,
                   AudioCaptureDefaults.BitsPerSample,
                   AudioCaptureDefaults.Channels)))
        {
            writer.Write(pcmBytes, 0, pcmBytes.Length);
            writer.Flush();
        }

        return output.ToArray();
    }

    private void CleanupCaptureUnsafe()
    {
        CleanupWaveInUnsafe();
        _currentChunkStream?.Dispose();
        _currentChunkStream = null;
    }

    private void CleanupWaveInUnsafe()
    {
        if (_waveIn is null)
        {
            return;
        }

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioService));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (IsRecording && _waveIn is not null)
                {
                    _waveIn.StopRecording();
                }
            }
            catch
            {
                // best effort during dispose
            }

            CleanupCaptureUnsafe();
            IsRecording = false;
            _disposed = true;
        }
    }
}
#endif
