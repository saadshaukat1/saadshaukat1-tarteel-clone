#if WINDOWS
using NAudio.Wave;

namespace TarteelMobile.Services;

public static partial class AudioPreprocessor
{
    public static byte[] WriteWavFile(byte[] pcmBytes, int sampleRate, short bitsPerSample, short channels)
    {
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, new WaveFormat(sampleRate, bitsPerSample, channels)))
        {
            writer.Write(pcmBytes, 0, pcmBytes.Length);
            writer.Flush();
        }
        return output.ToArray();
    }
}
#endif
