using System.Globalization;
using System.Text;
using TarteelClone.LocalRecitationCore.Abstractions;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelClone.LocalRecitationCore.Services;

/// <summary>
/// Minimal local recitation pipeline for offline desktop scenarios.
/// It is intentionally simple so parallel audio/asr/sqlite phases can extend it.
/// </summary>
public sealed class OfflineRecitationOrchestrator : IRecitationOrchestrator
{
    private readonly IAsrEngine _asrEngine;
    private readonly IVerseMatcher _verseMatcher;
    private readonly IProgressStore _progressStore;
    private readonly object _sync = new();
    private readonly object _transcriptSync = new();
    private readonly List<string> _sessionTranscriptTokens = [];
    private bool _isRunning;
    private long _submittedCount;
    private long _emptyTranscriptCount;
    private long _matchCount;

    public OfflineRecitationOrchestrator(
        IAsrEngine asrEngine,
        IVerseMatcher verseMatcher,
        IProgressStore progressStore)
    {
        _asrEngine = asrEngine;
        _verseMatcher = verseMatcher;
        _progressStore = progressStore;
    }

    public event EventHandler<RecitationMatchResult>? MatchProduced;
    public event EventHandler<string>? DiagnosticEmitted;

    public void SetSurahContext(int surahNum)
    {
        _verseMatcher.SetSurahContext(surahNum);
    }

    public void ClearSurahContext()
    {
        _verseMatcher.ClearSurahContext();
    }

    public void ClearLastMatchedPosition()
    {
        _verseMatcher.ClearLastMatchedPosition();
    }

    public void MarkCurrentAyahComplete()
    {
        _verseMatcher.MarkCurrentAyahComplete();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_transcriptSync)
        {
            _sessionTranscriptTokens.Clear();
        }

        try
        {
            await _asrEngine.InitializeAsync(cancellationToken);
        }
        catch
        {
            lock (_sync)
            {
                _isRunning = false;
            }
            throw;
        }

        lock (_sync)
        {
            _isRunning = true;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isRunning = false;
        }
        lock (_transcriptSync)
        {
            _sessionTranscriptTokens.Clear();
        }
        _verseMatcher.ClearLastMatchedPosition();

        await Task.CompletedTask;
    }

    public async Task SubmitAudioChunkAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default)
    {
        if (audioChunk is null || audioChunk.Length == 0)
        {
            return;
        }

        bool isRunning;
        lock (_sync)
        {
            isRunning = _isRunning;
        }

        if (!isRunning)
        {
            return;
        }

        var submitted = Interlocked.Increment(ref _submittedCount);
        var transcription = await _asrEngine.TranscribeAsync(audioChunk, cancellationToken);

        // ── Emit a diagnostic line after every attempt so the UI can show live status ──
        var isMock = transcription.DiagnosticMessage?.Contains("mock", StringComparison.OrdinalIgnoreCase) == true
                  || transcription.DiagnosticMessage?.Contains("Mock transcript", StringComparison.OrdinalIgnoreCase) == true;
        var diagLine = transcription.IsSuccess
            ? $"✅ chunk #{submitted} | tier: {transcription.TierUsed} | transcript: \"{transcription.Text?.Trim()}\""
            : isMock
                ? $"⚠️ chunk #{submitted} | MOCK MODE — whisper-cli.exe or model not found. Check offline/extras & offline/models folders."
                : $"❌ chunk #{submitted} | tier: {transcription.TierUsed} | {transcription.DiagnosticMessage}";
        DiagnosticEmitted?.Invoke(this, diagLine);

        if (!transcription.IsSuccess)
        {
            return;
        }

        var transcriptionText = transcription.Text?.Trim();
        if (string.IsNullOrWhiteSpace(transcriptionText))
        {
            var emptyCount = Interlocked.Increment(ref _emptyTranscriptCount);
            DiagnosticEmitted?.Invoke(this,
                $"🔇 chunk #{submitted} | Whisper returned empty transcript (total empty: {emptyCount}, tier: {transcription.TierUsed})");
            return;
        }

        var aggregatedTranscript = MergeTranscript(transcriptionText);
        if (string.IsNullOrWhiteSpace(aggregatedTranscript))
        {
            return;
        }

        var match = await _verseMatcher.MatchAsync(aggregatedTranscript, cancellationToken);
        var matchCount = Interlocked.Increment(ref _matchCount);

        var phonemeViolations = TajweedPhonemeAnalyzer.Analyze(
            audioChunk,
            match.ArabicText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            transcription.WordTimestamps);

        var combinedViolations = MergeViolations(match.TajweedViolations, phonemeViolations);

        DiagnosticEmitted?.Invoke(this,
            $"🎯 match #{matchCount} | {match.SurahNum}:{match.AyahNum} conf={match.Confidence:0.00} matched={match.MatchedWordCount}/{match.ProcessedWordCount} mismatches={match.Mismatches.Count} tajweed={combinedViolations.Count}");

        var matchWithTranscript = new RecitationMatchResult
        {
            SurahNum = match.SurahNum,
            AyahNum = match.AyahNum,
            ArabicText = match.ArabicText,
            TranscriptionText = aggregatedTranscript,
            Confidence = match.Confidence,
            ProcessedWordCount = match.ProcessedWordCount,
            MatchedWordCount = match.MatchedWordCount,
            JuzNum = match.JuzNum,
            SurahNameEnglish = match.SurahNameEnglish,
            SurahNameArabic = match.SurahNameArabic,
            Mismatches = match.Mismatches,
            TajweedViolations = combinedViolations,
            SpokenToExpectedPosition = match.SpokenToExpectedPosition
        };

        await _progressStore.SaveAsync(matchWithTranscript, cancellationToken);

        if (match.Confidence >= 0.65)
        {
            _verseMatcher.SetLastMatchedPosition(match.SurahNum, match.AyahNum);
        }

        MatchProduced?.Invoke(this, matchWithTranscript);
    }

    private string MergeTranscript(string newTranscript)
    {
        var incomingTokens = TokenizeTranscript(newTranscript);
        if (incomingTokens.Count == 0)
        {
            return string.Empty;
        }

        lock (_transcriptSync)
        {
            if (_sessionTranscriptTokens.Count == 0)
            {
                _sessionTranscriptTokens.AddRange(incomingTokens);
                return string.Join(' ', _sessionTranscriptTokens);
            }

            var overlap = FindOverlap(_sessionTranscriptTokens, incomingTokens);
            for (var index = overlap; index < incomingTokens.Count; index++)
            {
                _sessionTranscriptTokens.Add(incomingTokens[index]);
            }

            // Cap the rolling window at 32 tokens so the matcher's Needleman–Wunsch
            // alignment (O(n·m)) stays cheap on weak hardware. 32 tokens covers ~1–2
            // ayahs of recitation, which is enough context for accurate matching.
            if (_sessionTranscriptTokens.Count > 32)
            {
                _sessionTranscriptTokens.RemoveRange(0, _sessionTranscriptTokens.Count - 32);
            }

            return string.Join(' ', _sessionTranscriptTokens);
        }
    }

    private static List<string> TokenizeTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return [];
        }

        return transcript
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static int FindOverlap(IReadOnlyList<string> existingTokens, IReadOnlyList<string> incomingTokens)
    {
        var maxOverlap = Math.Min(existingTokens.Count, incomingTokens.Count);
        for (var overlap = maxOverlap; overlap > 0; overlap--)
        {
            var isMatch = true;
            for (var offset = 0; offset < overlap; offset++)
            {
                var existingToken = NormalizeToken(existingTokens[existingTokens.Count - overlap + offset]);
                var incomingToken = NormalizeToken(incomingTokens[offset]);
                if (!string.Equals(existingToken, incomingToken, StringComparison.Ordinal))
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                return overlap;
            }
        }

        return 0;
    }

    private static List<TajweedViolation> MergeViolations(
        IReadOnlyList<TajweedViolation> textViolations,
        IReadOnlyList<TajweedViolation> phonemeViolations)
    {
        var seen = new HashSet<(int Position, TajweedRuleType Rule)>();
        var result = new List<TajweedViolation>(textViolations.Count + phonemeViolations.Count);

        // Prefer phoneme (audio-evidence) violations: add them first so text duplicates get skipped.
        foreach (var v in phonemeViolations)
        {
            if (seen.Add((v.WordPosition, v.Rule)))
                result.Add(v);
        }

        foreach (var v in textViolations)
        {
            if (seen.Add((v.WordPosition, v.Rule)))
                result.Add(v);
        }

        return result;
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(token.Length);
        foreach (var character in token.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mappedCharacter = character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ؤ' => 'و',
                'ئ' or 'ى' => 'ي',
                'ة' => 'ه',
                'ـ' => '\0',
                _ => character
            };

            if (mappedCharacter == '\0')
            {
                continue;
            }

            if (char.IsLetterOrDigit(mappedCharacter) || char.IsWhiteSpace(mappedCharacter))
            {
                builder.Append(mappedCharacter);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
