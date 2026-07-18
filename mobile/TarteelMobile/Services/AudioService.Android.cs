#if ANDROID
using Android.Media;

namespace TarteelMobile.Services;

public sealed class AudioService : IAudioService, IDisposable
{
    private readonly object _sync = new();
    private AudioRecord? _audioRecord;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private bool _disposed;

    public event EventHandler<byte[]>? AudioChunkReady;
    public event EventHandler<Exception>? RecordingError;
    public bool IsRecording { get; private set; }

    public Task StartRecordingAsync()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (IsRecording)
                return Task.CompletedTask;

            var minBufferSize = AudioRecord.GetMinBufferSize(
                AudioCaptureDefaults.SampleRateHz,
                ChannelIn.Mono,
                Encoding.Pcm16bit);

            if (minBufferSize <= 0)
            {
                throw new InvalidOperationException(
                    "AudioRecord.GetMinBufferSize returned invalid size. " +
                    "Ensure RECORD_AUDIO permission is granted.");
            }

            var bufferSize = Math.Max(minBufferSize * 4,
                AudioCaptureDefaults.SampleRateHz *
                AudioCaptureDefaults.BitsPerSample / 8 *
                AudioCaptureDefaults.Channels *
                AudioCaptureDefaults.ChunkDurationMilliseconds / 1000);

            _audioRecord = new AudioRecord(
                AudioSource.Mic,
                AudioCaptureDefaults.SampleRateHz,
                ChannelIn.Mono,
                Encoding.Pcm16bit,
                bufferSize);

            if (_audioRecord.State != State.Initialized)
            {
                _audioRecord.Dispose();
                _audioRecord = null;
                throw new InvalidOperationException(
                    "AudioRecord failed to initialize. Check microphone permissions and availability.");
            }

            _captureCts = new CancellationTokenSource();
            _audioRecord.StartRecording();
            IsRecording = true;

            _captureTask = Task.Run(() => CaptureLoop(_captureCts.Token));
        }

        return Task.CompletedTask;
    }

    public Task StopRecordingAsync()
    {
        byte[]? finalChunk = null;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!IsRecording)
                return Task.CompletedTask;

            _captureCts?.Cancel();
            IsRecording = false;

            try
            {
                _audioRecord?.Stop();
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, new InvalidOperationException(
                    "Failed to stop microphone cleanly.", ex));
            }

            CleanupCapture();
        }

        if (finalChunk is { Length: > 0 })
            AudioChunkReady?.Invoke(this, finalChunk);

        return Task.CompletedTask;
    }

    private void CaptureLoop(CancellationToken cancellationToken)
    {
        var chunkDurationMs = AudioCaptureDefaults.ChunkDurationMilliseconds;
        var overlapDurationMs = AudioCaptureDefaults.ChunkOverlapMilliseconds;
        var bytesPerMs = AudioCaptureDefaults.SampleRateHz *
                         (AudioCaptureDefaults.BitsPerSample / 8) *
                         AudioCaptureDefaults.Channels / 1000;

        var chunkBuffer = new MemoryStream();
        var overlayBuffer = new MemoryStream();
        var readBuffer = new byte[bytesPerMs * 100]; // 100ms read buffer
        var chunkStartedAt = DateTimeOffset.UtcNow;
        var hasFreshAudio = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                AudioRecord? record;
                lock (_sync)
                {
                    record = _audioRecord;
                }

                if (record is null || record.RecordingState != RecordState.Recording)
                    break;

                var bytesRead = record.Read(readBuffer, 0, readBuffer.Length);
                if (bytesRead <= 0)
                    continue;

                chunkBuffer.Write(readBuffer, 0, bytesRead);
                hasFreshAudio = true;

                var elapsed = (DateTimeOffset.UtcNow - chunkStartedAt).TotalMilliseconds;
                if (elapsed >= chunkDurationMs)
                {
                    byte[] pcmBytes = chunkBuffer.ToArray();
                    byte[]? chunkToEmit = null;

                    if (pcmBytes.Length > 0 && hasFreshAudio)
                        chunkToEmit = BuildWav(pcmBytes);

                    // Compute overlap for next chunk
                    var overlapByteCount = Math.Min(
                        overlapDurationMs * bytesPerMs,
                        pcmBytes.Length);
                    overlapByteCount -= overlapByteCount % (AudioCaptureDefaults.BitsPerSample / 8);

                    chunkBuffer.Dispose();
                    chunkBuffer = new MemoryStream();

                    if (overlapByteCount > 0)
                    {
                        var overlap = new byte[overlapByteCount];
                        Array.Copy(pcmBytes, pcmBytes.Length - overlapByteCount,
                            overlap, 0, overlapByteCount);
                        chunkBuffer.Write(overlap, 0, overlap.Length);
                    }

                    chunkStartedAt = DateTimeOffset.UtcNow;
                    hasFreshAudio = false;

                    if (chunkToEmit is { Length: > 0 })
                        AudioChunkReady?.Invoke(this, chunkToEmit);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
        }
        catch (Exception ex)
        {
            RecordingError?.Invoke(this, new InvalidOperationException(
                "Microphone capture failed.", ex));
        }
        finally
        {
            chunkBuffer.Dispose();
            overlayBuffer.Dispose();
        }
    }

    private static byte[] BuildWav(byte[] pcmBytes)
    {
        const int sampleRate = AudioCaptureDefaults.SampleRateHz;
        const short bitsPerSample = AudioCaptureDefaults.BitsPerSample;
        const short channels = AudioCaptureDefaults.Channels;

        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var dataSize = pcmBytes.Length;
        var fileSize = 44 + dataSize;

        var wav = new byte[fileSize];
        var pos = 0;

        // RIFF header
        wav[pos++] = (byte)'R'; wav[pos++] = (byte)'I'; wav[pos++] = (byte)'F'; wav[pos++] = (byte)'F';
        wav[pos++] = (byte)((fileSize - 8) & 0xFF);
        wav[pos++] = (byte)(((fileSize - 8) >> 8) & 0xFF);
        wav[pos++] = (byte)(((fileSize - 8) >> 16) & 0xFF);
        wav[pos++] = (byte)(((fileSize - 8) >> 24) & 0xFF);
        wav[pos++] = (byte)'W'; wav[pos++] = (byte)'A'; wav[pos++] = (byte)'V'; wav[pos++] = (byte)'E';

        // fmt chunk
        wav[pos++] = (byte)'f'; wav[pos++] = (byte)'m'; wav[pos++] = (byte)'t'; wav[pos++] = (byte)' ';
        wav[pos++] = 16; wav[pos++] = 0; wav[pos++] = 0; wav[pos++] = 0; // chunk size
        wav[pos++] = 1; wav[pos++] = 0; // PCM format
        wav[pos++] = (byte)(channels & 0xFF); wav[pos++] = (byte)((channels >> 8) & 0xFF);
        wav[pos++] = (byte)(sampleRate & 0xFF); wav[pos++] = (byte)((sampleRate >> 8) & 0xFF);
        wav[pos++] = (byte)((sampleRate >> 16) & 0xFF); wav[pos++] = (byte)((sampleRate >> 24) & 0xFF);
        wav[pos++] = (byte)(byteRate & 0xFF); wav[pos++] = (byte)((byteRate >> 8) & 0xFF);
        wav[pos++] = (byte)((byteRate >> 16) & 0xFF); wav[pos++] = (byte)((byteRate >> 24) & 0xFF);
        wav[pos++] = (byte)(blockAlign & 0xFF); wav[pos++] = (byte)((blockAlign >> 8) & 0xFF);
        wav[pos++] = (byte)(bitsPerSample & 0xFF); wav[pos++] = (byte)((bitsPerSample >> 8) & 0xFF);

        // data chunk
        wav[pos++] = (byte)'d'; wav[pos++] = (byte)'a'; wav[pos++] = (byte)'t'; wav[pos++] = (byte)'a';
        wav[pos++] = (byte)(dataSize & 0xFF); wav[pos++] = (byte)((dataSize >> 8) & 0xFF);
        wav[pos++] = (byte)((dataSize >> 16) & 0xFF); wav[pos++] = (byte)((dataSize >> 24) & 0xFF);

        Array.Copy(pcmBytes, 0, wav, pos, dataSize);

        return wav;
    }

    private void CleanupCapture()
    {
        _audioRecord?.Dispose();
        _audioRecord = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioService));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _captureCts?.Cancel();
            _captureCts?.Dispose();
            _captureCts = null;

            try
            {
                if (IsRecording && _audioRecord is not null)
                    _audioRecord.Stop();
            }
            catch
            {
                // best effort
            }

            CleanupCapture();
            IsRecording = false;
            _disposed = true;
        }
    }
}
#endif
