namespace TarteelMobile.Services;

/// <summary>
/// Lightweight audio preprocessing for 16-bit 16kHz mono PCM.
/// Handles both raw PCM and WAV-formatted input. Applied before Whisper
/// inference to reduce hallucination and normalize levels.
/// </summary>
public static partial class AudioPreprocessor
{
    private const double SilenceRmsThreshold = 120.0;
    private const double TargetRms = 1800.0;

    public static bool IsSilence(byte[] audioData)
    {
        if (audioData is null || audioData.Length == 0)
            return true;

        var pcm = ExtractPcm(audioData);
        if (pcm.Length == 0)
            return true;

        var sampleCount = pcm.Length / 2;
        double sumSquares = 0;

        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            sumSquares += sample * sample;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        return rms < SilenceRmsThreshold;
    }

    public static byte[] NormalizeRms(byte[] audioData)
    {
        if (audioData is null || audioData.Length == 0)
            return audioData ?? [];

        var isWav = IsWavFile(audioData);
        byte[] pcm;
        int headerSize;

        if (isWav)
        {
            headerSize = FindDataChunkOffset(audioData);
            if (headerSize < 0)
                return audioData;
            pcm = audioData[headerSize..];
        }
        else
        {
            headerSize = 0;
            pcm = audioData;
        }

        var sampleCount = pcm.Length / 2;
        if (sampleCount == 0)
            return audioData;

        double sumSquares = 0;

        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            sumSquares += sample * sample;
        }

        var currentRms = Math.Sqrt(sumSquares / sampleCount);
        if (currentRms < 1.0)
            return audioData;

        var gain = TargetRms / currentRms;
        if (gain is > 0.99 and < 1.01)
            return audioData;

        var normalizedPcm = new byte[pcm.Length];
        for (var i = 0; i < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            var normalized = (short)Math.Clamp(sample * gain, short.MinValue, short.MaxValue);
            normalizedPcm[i] = (byte)(normalized & 0xFF);
            normalizedPcm[i + 1] = (byte)((normalized >> 8) & 0xFF);
        }

        if (!isWav)
            return normalizedPcm;

        return WriteWavFile(normalizedPcm,
            AudioCaptureDefaults.SampleRateHz,
            AudioCaptureDefaults.BitsPerSample,
            AudioCaptureDefaults.Channels);
    }

    private static byte[] ExtractPcm(byte[] audioData)
    {
        if (!IsWavFile(audioData))
            return audioData;

        var offset = FindDataChunkOffset(audioData);
        return offset >= 0 ? audioData[offset..] : [];
    }

    private static bool IsWavFile(byte[] data)
    {
        return data.Length >= 12
            && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E';
    }

    private static int FindDataChunkOffset(byte[] wav)
    {
        var pos = 12;

        while (pos + 8 <= wav.Length)
        {
            var chunkId = (char)wav[pos] + "" + (char)wav[pos + 1] + "" + (char)wav[pos + 2] + "" + (char)wav[pos + 3];
            var chunkSize = BitConverter.ToInt32(wav, pos + 4);

            if (chunkId == "data")
                return pos + 8;

            pos += 8 + chunkSize;
        }

        return -1;
    }
}
