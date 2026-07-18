#if ANDROID
namespace TarteelMobile.Services;

public static partial class AudioPreprocessor
{
    public static byte[] WriteWavFile(byte[] pcmBytes, int sampleRate, short bitsPerSample, short channels)
    {
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
        wav[pos++] = 16; wav[pos++] = 0; wav[pos++] = 0; wav[pos++] = 0;
        wav[pos++] = 1; wav[pos++] = 0;
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
}
#endif
