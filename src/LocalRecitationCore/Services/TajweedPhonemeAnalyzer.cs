using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Audio-signal-level tajweed analysis complementing the text-level TajweedRuleEngine.
/// Analyses PCM audio alongside word timestamps to detect timing, resonance, and
/// articulation violations that word-level comparison cannot catch.
/// </summary>
public static class TajweedPhonemeAnalyzer
{
    // Madd letters: alif, waw, yaa when they function as elongation vowels.
    private static readonly HashSet<char> MaddLetters = new() { 'ا', 'و', 'ي' };

    // Qalqalah letters produce a plosive echo when stopped.
    private static readonly HashSet<char> QalqalahLetters = new() { 'ق', 'ط', 'ب', 'ج', 'د' };

    // Ghunna source letters: nun and mim.
    private static readonly HashSet<char> GhunnaLetters = new() { 'ن', 'م' };

    // One harakah ≈ 250–500 ms depending on recitation speed.
    // Madd requires 2–6 harakat, so the expected vowel duration range is:
    private const double MinHarakahMs = 250;
    private const double MaxHarakahMs = 500;
    private const int MinMaddHarakat = 2;
    private const int MaxMaddHarakat = 6;

    // Spectral centroid thresholds for ghunna (nasal) detection.
    // Nasal sounds have energy weighted toward lower frequencies.
    private const double GhunnaSpectralCentroidMax = 0.35;

    // Qalqalah: plosive burst is a short, high-amplitude spike.
    private const double QalqalahRmsBurstRatio = 4.0;
    private const int QalqalahBurstWindowSamples = 200;
    private const int QalqalahPostBurstSilenceSamples = 400;

    /// <summary>
    /// Analyses the raw PCM audio chunk for phoneme-level tajweed violations,
    /// producing additional violations beyond the text-level engine.
    /// </summary>
    public static IReadOnlyList<TajweedViolation> Analyze(
        byte[] audioChunk,
        IReadOnlyList<string> expectedWords,
        IReadOnlyList<RecitationWordTimestamp> wordTimestamps,
        int sampleRate = 16000,
        short bitsPerSample = 16)
    {
        if (audioChunk.Length == 0 || expectedWords.Count == 0 || wordTimestamps.Count == 0)
            return [];

        var violations = new List<TajweedViolation>();
        var pcm = ExtractPcmSamples(audioChunk);

        for (var pos = 0; pos < Math.Min(expectedWords.Count, wordTimestamps.Count); pos++)
        {
            var expected = expectedWords[pos];
            var timestamp = wordTimestamps[pos];

            var wordSamples = SliceSamples(pcm, timestamp.Start, timestamp.End, sampleRate);
            if (wordSamples.Length == 0)
                continue;

            var maddViolation = CheckMadd(expected, wordSamples, timestamp, pos, sampleRate);
            if (maddViolation is not null)
            {
                violations.Add(maddViolation);
            }

            var ghunnaViolation = CheckGhunna(expected, wordSamples, pos, sampleRate);
            if (ghunnaViolation is not null)
            {
                violations.Add(ghunnaViolation);
            }

            var qalqalahViolation = CheckQalqalah(expected, wordSamples, pos, sampleRate);
            if (qalqalahViolation is not null)
            {
                violations.Add(qalqalahViolation);
            }
        }

        return violations;
    }

    private static TajweedViolation? CheckMadd(
        string expectedWord,
        float[] wordSamples,
        RecitationWordTimestamp timestamp,
        int position,
        int sampleRate)
    {
        if (!ContainsMaddLetter(expectedWord))
            return null;

        var durationMs = (timestamp.End - timestamp.Start).TotalMilliseconds;
        var minDurationMs = MinMaddHarakat * MinHarakahMs;
        var maxDurationMs = MaxMaddHarakat * MaxHarakahMs;

        if (durationMs >= minDurationMs && durationMs <= maxDurationMs)
            return null;

        var status = durationMs < minDurationMs ? "too short" : "too long";
        return new TajweedViolation(
            position,
            TajweedRuleType.Madd,
            expectedWord,
            expectedWord,
            $"Madd in '{expectedWord}' is {durationMs:0} ms ({status}). Expected {minDurationMs:0}–{maxDurationMs:0} ms for 2–6 harakat.");
    }

    private static TajweedViolation? CheckGhunna(
        string expectedWord,
        float[] wordSamples,
        int position,
        int sampleRate)
    {
        if (!ContainsGhunnaLetter(expectedWord))
            return null;

        var centroid = ComputeSpectralCentroid(wordSamples, sampleRate);
        if (centroid <= GhunnaSpectralCentroidMax)
            return null;

        return new TajweedViolation(
            position,
            TajweedRuleType.Ghunna,
            expectedWord,
            expectedWord,
            $"Ghunna in '{expectedWord}' may be missing. Nasal resonance expected (centroid {centroid:0.00} exceeds nasal threshold {GhunnaSpectralCentroidMax:0.00}).");
    }

    private static TajweedViolation? CheckQalqalah(
        string expectedWord,
        float[] wordSamples,
        int position,
        int sampleRate)
    {
        if (!EndsWithQalqalahLetter(expectedWord))
            return null;

        var (burstDetected, burstStrength) = DetectPlosiveBurst(wordSamples);
        if (burstDetected)
            return null;

        return new TajweedViolation(
            position,
            TajweedRuleType.Qalqalah,
            expectedWord,
            expectedWord,
            $"Qalqalah echo expected at the end of '{expectedWord}'. Apply a short bounce/echo on the stopping letter.");
    }

    private static bool ContainsMaddLetter(string word)
    {
        foreach (var ch in word)
            if (MaddLetters.Contains(ch))
                return true;
        return false;
    }

    private static bool ContainsGhunnaLetter(string word)
    {
        foreach (var ch in word)
            if (GhunnaLetters.Contains(ch))
                return true;
        return false;
    }

    private static bool EndsWithQalqalahLetter(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;
        return QalqalahLetters.Contains(word[^1]);
    }

    private static float[] ExtractPcmSamples(byte[] audioData)
    {
        var sampleCount = audioData.Length / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < audioData.Length; i += 2)
        {
            var sample = (short)(audioData[i] | (audioData[i + 1] << 8));
            samples[i / 2] = sample / 32768f;
        }
        return samples;
    }

    private static float[] SliceSamples(float[] pcm, TimeSpan start, TimeSpan end, int sampleRate)
    {
        var startIndex = (int)(start.TotalSeconds * sampleRate);
        var endIndex = (int)(end.TotalSeconds * sampleRate);
        startIndex = Math.Clamp(startIndex, 0, pcm.Length - 1);
        endIndex = Math.Clamp(endIndex, startIndex + 1, pcm.Length);

        var slice = new float[endIndex - startIndex];
        Array.Copy(pcm, startIndex, slice, 0, slice.Length);
        return slice;
    }

    private static double ComputeSpectralCentroid(float[] samples, int sampleRate)
    {
        if (samples.Length < 2)
            return 0;

        var window = samples.Take(Math.Min(samples.Length, 1024)).ToArray();
        var fftSize = NextPowerOfTwo(window.Length);
        var real = new double[fftSize];
        var imag = new double[fftSize];

        for (var i = 0; i < window.Length; i++)
            real[i] = window[i];

        Fft(real, imag);

        var weightedSum = 0.0;
        var totalMagnitude = 0.0;
        for (var i = 1; i < fftSize / 2; i++)
        {
            var magnitude = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            var frequency = (double)i * sampleRate / fftSize;
            weightedSum += frequency * magnitude;
            totalMagnitude += magnitude;
        }

        return totalMagnitude > 0 ? weightedSum / totalMagnitude / sampleRate : 0;
    }

    private static (bool Detected, double Strength) DetectPlosiveBurst(float[] samples)
    {
        if (samples.Length < QalqalahBurstWindowSamples + QalqalahPostBurstSilenceSamples)
            return (false, 0);

        var endRegion = samples[^QalqalahPostBurstSilenceSamples..];
        var endRms = ComputeRms(endRegion);

        for (var offset = QalqalahPostBurstSilenceSamples;
             offset < QalqalahPostBurstSilenceSamples + QalqalahBurstWindowSamples && offset < samples.Length;
             offset++)
        {
            var start = samples.Length - offset - QalqalahBurstWindowSamples;
            if (start < 0) break;

            var burstWindow = new float[QalqalahBurstWindowSamples];
            Array.Copy(samples, start, burstWindow, 0, QalqalahBurstWindowSamples);
            var burstRms = ComputeRms(burstWindow);

            if (endRms > 0 && burstRms / endRms >= QalqalahRmsBurstRatio)
                return (true, burstRms / Math.Max(endRms, 0.001));
        }

        return (false, 0);
    }

    private static double ComputeRms(float[] samples)
    {
        if (samples.Length == 0)
            return 0;

        var sumSquares = 0.0;
        for (var i = 0; i < samples.Length; i++)
            sumSquares += samples[i] * samples[i];

        return Math.Sqrt(sumSquares / samples.Length);
    }

    private static int NextPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
            power <<= 1;
        return power;
    }

    /// <summary>Simple in-place Cooley-Tukey FFT (radix-2, decimation in time).</summary>
    private static void Fft(double[] real, double[] imag)
    {
        var n = real.Length;
        var bits = (int)Math.Log2(n);

        for (var i = 0; i < n; i++)
        {
            var j = BitReverse(i, bits);
            if (j > i)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (var size = 2; size <= n; size <<= 1)
        {
            var half = size / 2;
            var angle = -2.0 * Math.PI / size;

            for (var i = 0; i < n; i += size)
            {
                for (var k = 0; k < half; k++)
                {
                    var wReal = Math.Cos(angle * k);
                    var wImag = Math.Sin(angle * k);

                    var tReal = wReal * real[i + k + half] - wImag * imag[i + k + half];
                    var tImag = wReal * imag[i + k + half] + wImag * real[i + k + half];

                    real[i + k + half] = real[i + k] - tReal;
                    imag[i + k + half] = imag[i + k] - tImag;
                    real[i + k] += tReal;
                    imag[i + k] += tImag;
                }
            }
        }
    }

    private static int BitReverse(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }
}
