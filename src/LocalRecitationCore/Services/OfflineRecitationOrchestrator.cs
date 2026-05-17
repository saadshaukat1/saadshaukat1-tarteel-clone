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

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isRunning = true;
        }
        lock (_transcriptSync)
        {
            _sessionTranscriptTokens.Clear();
        }

        await _asrEngine.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _isRunning = false;
        }
        lock (_transcriptSync)
        {
            _sessionTranscriptTokens.Clear();
        }

        return Task.CompletedTask;
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
        if (!transcription.IsSuccess)
        {
            return;
        }

        var transcriptionText = transcription.Text?.Trim();
        if (string.IsNullOrWhiteSpace(transcriptionText))
        {
            var emptyCount = Interlocked.Increment(ref _emptyTranscriptCount);
            if (emptyCount <= 5 || emptyCount % 10 == 0)
            {
                // Light-weight, throttled instrumentation for "why no UI updates?" debugging.
                Console.WriteLine(
                    $"[recitation] chunk#{submitted} produced empty transcript (tier={transcription.TierUsed}, " +
                    $"mock={transcription.DiagnosticMessage?.Contains("Mock transcript mode active", StringComparison.OrdinalIgnoreCase) == true}).");
            }
            return;
        }

        var aggregatedTranscript = MergeTranscript(transcriptionText);
        if (string.IsNullOrWhiteSpace(aggregatedTranscript))
        {
            return;
        }

        var match = await _verseMatcher.MatchAsync(aggregatedTranscript, cancellationToken);
        var matchCount = Interlocked.Increment(ref _matchCount);
        if (matchCount <= 5 || matchCount % 10 == 0)
        {
            Console.WriteLine(
                $"[recitation] match#{matchCount} s={match.SurahNum}:{match.AyahNum} conf={match.Confidence:0.00} " +
                $"processed={match.ProcessedWordCount} matched={match.MatchedWordCount} mismatches={match.Mismatches.Count}");
        }

        await _progressStore.SaveAsync(match, cancellationToken);
        MatchProduced?.Invoke(this, match);
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

            if (_sessionTranscriptTokens.Count > 48)
            {
                _sessionTranscriptTokens.RemoveRange(0, _sessionTranscriptTokens.Count - 48);
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
