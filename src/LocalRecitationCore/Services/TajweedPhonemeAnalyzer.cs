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

    // Idgham target letters (with ghunna): the letters ي, ر, م, ل, و, ن after a nun-sakinah merge.
    private static readonly HashSet<char> IdghamWithGhunnaLetters = new() { 'ي', 'ر', 'م', 'ل', 'و', 'ن' };

    // Idgham target letters (without ghunna): ل, ر after nun-sakinah merge without nasalization.
    private static readonly HashSet<char> IdghamWithoutGhunnaLetters = new() { 'ل', 'ر' };

    // Ikhfa letters: the 15 letters after which nun-sakinah/tanween is concealed with nasalization.
    private static readonly HashSet<char> IkhfaLetters = new()
        { 'ت', 'ث', 'ج', 'د', 'ذ', 'ز', 'س', 'ش', 'ص', 'ض', 'ط', 'ظ', 'ف', 'ق', 'ك' };

    // Common makhraj-confusion pairs: letters that share similar articulation points and are often confused.
    // Key = actual letter, Value = common confusion letter.
    private static readonly Dictionary<char, char> MakhrajConfusions = new()
    {
        ['ح'] = 'خ', ['خ'] = 'ح',
        ['س'] = 'ص', ['ص'] = 'س',
        ['ذ'] = 'ز', ['ز'] = 'ذ',
        ['ظ'] = 'ض', ['ض'] = 'ظ',
        ['ق'] = 'ك', ['ك'] = 'ق',
        ['ط'] = 'ت', ['ت'] = 'ط',
    };

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

            var idghamViolation = CheckIdgham(expected, pos, expectedWords, wordSamples, sampleRate);
            if (idghamViolation is not null)
            {
                violations.Add(idghamViolation);
            }

            var ikhfaViolation = CheckIkhfa(expected, pos, expectedWords, wordSamples, sampleRate);
            if (ikhfaViolation is not null)
            {
                violations.Add(ikhfaViolation);
            }

            var makhrajViolation = CheckMakhraj(expected, wordSamples, pos, sampleRate);
            if (makhrajViolation is not null)
            {
                violations.Add(makhrajViolation);
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
            $"Madd in '{expectedWord}' may be {durationMs:0} ms ({status}). Expected {minDurationMs:0}–{maxDurationMs:0} ms for 2–6 harakat.");
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
        var stripped = TajweedRuleEngine.Strip(word);
        return stripped.Length > 0 && QalqalahLetters.Contains(stripped[^1]);
    }

    private static TajweedViolation? CheckIdgham(
        string expectedWord,
        int position,
        IReadOnlyList<string> expectedWords,
        float[] wordSamples,
        int sampleRate)
    {
        // Idgham: nun-sakinah or tanween merges into the following letter.
        // Check if the current word ends with a nun-sakinah marker and the next word starts with an idgham letter.
        var normalized = TajweedRuleEngine.Strip(expectedWord);
        if (normalized.Length == 0 || normalized[^1] != 'ن' || position + 1 >= expectedWords.Count)
            return null;

        var nextWord = expectedWords[position + 1];
        var nextStripped = TajweedRuleEngine.Strip(nextWord);
        if (nextStripped.Length == 0)
            return null;

        var nextFirstLetter = nextStripped[0];
        if (!IdghamWithGhunnaLetters.Contains(nextFirstLetter))
            return null;

        var withGhunna = IdghamWithGhunnaLetters.Contains(nextFirstLetter) && !IdghamWithoutGhunnaLetters.Contains(nextFirstLetter);
        var centroid = ComputeSpectralCentroid(wordSamples, sampleRate);

        if (withGhunna && centroid <= GhunnaSpectralCentroidMax)
            return null; // Nasal resonance detected — correct idgham with ghunna.

        if (!withGhunna && centroid > GhunnaSpectralCentroidMax)
            return null; // No nasal resonance — correct idgham without ghunna.

        var hint = withGhunna
            ? $"Idgham with ghunna expected: '{normalized}' merges into '{nextStripped}' with nasal resonance."
            : $"Idgham without ghunna expected: '{normalized}' merges into '{nextStripped}' without nasal resonance.";

        return new TajweedViolation(position, TajweedRuleType.Idgham, expectedWord, expectedWord, hint);
    }

    private static TajweedViolation? CheckIkhfa(
        string expectedWord,
        int position,
        IReadOnlyList<string> expectedWords,
        float[] wordSamples,
        int sampleRate)
    {
        // Ikhfa: nun-sakinah or tanween is concealed with nasalization before ikhfa letters.
        var normalized = TajweedRuleEngine.Strip(expectedWord);
        if (normalized.Length == 0 || normalized[^1] != 'ن' || position + 1 >= expectedWords.Count)
            return null;

        var nextWord = expectedWords[position + 1];
        var nextStripped = TajweedRuleEngine.Strip(nextWord);
        if (nextStripped.Length == 0)
            return null;

        if (!IkhfaLetters.Contains(nextStripped[0]))
            return null;

        var centroid = ComputeSpectralCentroid(wordSamples, sampleRate);
        if (centroid <= GhunnaSpectralCentroidMax)
            return null; // Nasal concealment detected.

        return new TajweedViolation(
            position,
            TajweedRuleType.Ikhfa,
            expectedWord,
            expectedWord,
            $"Ikhfa expected: conceal '{normalized}' before '{nextStripped}' with a nasal hum.");
    }

    private static TajweedViolation? CheckMakhraj(
        string expectedWord,
        float[] wordSamples,
        int position,
        int sampleRate)
    {
        // Makhraj: check for common articulation-point confusion.
        // Uses spectral features to detect if a letter might have been mispronounced.
        var normalized = TajweedRuleEngine.Strip(expectedWord);
        if (normalized.Length == 0)
            return null;

        // Check for common confusion pairs.
        foreach (var ch in normalized)
        {
            if (MakhrajConfusions.TryGetValue(ch, out var confusedWith))
            {
                // Perform basic spectral check: if the spectral centroid is significantly
                // different from the expected range for this letter class, flag it.
                var centroid = ComputeSpectralCentroid(wordSamples, sampleRate);

                // Heuristic: throaty letters (ح, خ, ه, غ, ع) tend to have lower spectral centroid.
                var isThroaty = ch is 'ح' or 'خ' or 'ه' or 'غ' or 'ع';
                var isEmphatic = ch is 'ص' or 'ض' or 'ط' or 'ظ';

                if (isThroaty && centroid > 0.25)
                {
                    return new TajweedViolation(
                        position,
                        TajweedRuleType.MakhrajError,
                        expectedWord,
                        expectedWord,
                        $"Possible articulation error in '{normalized}': '{ch}' should be from deep throat (spectral centroid {centroid:0.00}). Do not replace with '{confusedWith}'.");
                }

                if (isEmphatic && centroid < 0.15)
                {
                    return new TajweedViolation(
                        position,
                        TajweedRuleType.MakhrajError,
                        expectedWord,
                        expectedWord,
                        $"Possible articulation error in '{normalized}': '{ch}' should be emphatic/heavy. Avoid the light version '{confusedWith}'.");
                }

                break; // One flag per word is enough.
            }
        }

        return null;
    }

    private static float[] ExtractPcmSamples(byte[] audioData)
    {
        var data = SkipWavHeader(audioData);
        var sampleCount = data.Length / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < data.Length; i += 2)
        {
            var sample = (short)(data[i] | (data[i + 1] << 8));
            samples[i / 2] = sample / 32768f;
        }
        return samples;
    }

    private static byte[] SkipWavHeader(byte[] data)
    {
        if (data.Length < 44)
            return data;

        // RIFF WAVE header: 'RIFF' at offset 0, 'WAVE' at offset 8.
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
            return data;
        if (data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
            return data;

        // Parse RIFF chunks to find the 'data' chunk.
        var pos = 12;
        while (pos + 8 <= data.Length)
        {
            var chunkSize = BitConverter.ToInt32(data, pos + 4);
            // Check for 'data' marker.
            if (data[pos] == 'd' && data[pos + 1] == 'a' && data[pos + 2] == 't' && data[pos + 3] == 'a')
                return data[(pos + 8)..];

            pos += 8 + chunkSize;
        }

        return data;
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

        var window = samples;            // Use the full word slice, not just first 1024 samples.
        if (window.Length > 4096)        // Cap at 4096 for performance.
            window = new ArraySegment<float>(samples, 0, 4096).ToArray();

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
