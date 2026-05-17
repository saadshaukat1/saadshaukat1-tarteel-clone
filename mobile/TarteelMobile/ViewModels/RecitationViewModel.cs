using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

/// <summary>
/// Drives the real-time recitation screen:
/// - Starts/stops mic recording
/// - Streams audio chunks through local in-process recitation pipeline
/// - Shows the matched verse with highlighted mismatches
/// </summary>
public partial class RecitationViewModel : ObservableObject
{
    private const double MatchConfidenceThreshold = 0.65;
    private const double PerfectConfidenceThreshold = 0.90;

    private readonly IAudioService      _audio;
    private readonly IRecitationService _recitation;
    private readonly IVerseRepository _verseRepository;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;
    private long _chunkDispatchCount;
    private MatchResult? _bestSessionResult;
    private int _bestPrefixLength;
    private int _bestMatchedWordCount;
    private int _bestProcessedWordCount;
    private double _bestConfidence;

    [ObservableProperty]
    private string _arabicText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Tap the mic to start reciting";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private List<WordMismatch> _mismatches = [];

    [ObservableProperty]
    private FormattedString _highlightedArabicText = new();

    [ObservableProperty]
    private string _processingSummary = "Awaiting audio processing…";

    public RecitationViewModel(IAudioService audio,
        IRecitationService recitation,
        IVerseRepository verseRepository,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _audio      = audio;
        _recitation = recitation;
        _verseRepository = verseRepository;
        _session = session;
        _diagnostics = diagnostics;

        _audio.AudioChunkReady        += OnAudioChunkReady;
        _audio.RecordingError         += OnAudioRecordingError;
        _recitation.MatchResultReceived += OnMatchResultReceived;
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (_recitation.RequiresAuthentication && !_session.IsAuthenticated)
        {
            StatusMessage = "Please log in first";
            return;
        }

        try
        {
            if (IsRecording)
            {
                await _audio.StopRecordingAsync();
                using (var flushTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                {
                    try
                    {
                        await _recitation.FlushAsync(flushTimeout.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _diagnostics.Warn("Timed out waiting for final ASR chunk while stopping recitation.");
                    }
                }
                await _recitation.DisconnectAsync();
                IsRecording    = false;
                StatusMessage  = "Recitation paused";
                _diagnostics.Info("Recitation recording paused.");
            }
            else
            {
                // Start each recording session from a clean UI baseline.
                Confidence = 0;
                ArabicText = string.Empty;
                Mismatches = [];
                HighlightedArabicText = new FormattedString();
                Interlocked.Exchange(ref _chunkDispatchCount, 0);
                _bestSessionResult = null;
                _bestPrefixLength = 0;
                _bestMatchedWordCount = 0;
                _bestProcessedWordCount = 0;
                _bestConfidence = 0;
                ProcessingSummary = "Connecting recitation pipeline…";
                await _recitation.ConnectAsync();
                try
                {
                    await _audio.StartRecordingAsync();
                }
                catch
                {
                    await _recitation.DisconnectAsync();
                    throw;
                }

                // Placeholder matcher targets 1:1 and ASR may produce no transcript for some time;
                // show expected verse immediately so the screen is never blank during "Listening…".
                await LoadPracticeVersePlaceholderAsync();

                IsRecording   = true;
                StatusMessage = "Listening… recite aloud—scores update after speech is recognized.";
                ProcessingSummary = "Audio stream active. Waiting for first recognized words…";
                _diagnostics.Info("Recitation recording started.");
            }
        }
        catch (Exception ex)
        {
            var rootCause = ex.GetBaseException();
            var userMessage = ReferenceEquals(rootCause, ex)
                ? $"Mic start failed: {ex.Message}"
                : $"Mic start failed: {ex.Message} ({rootCause.GetType().Name}: {rootCause.Message})";
            SetMicFailureState(userMessage);
            _diagnostics.Error("Failed to toggle recitation recording.", ex);
        }
    }

    private async void OnAudioChunkReady(object? sender, byte[] chunk)
    {
        try
        {
            var chunkNumber = Interlocked.Increment(ref _chunkDispatchCount);
            if (chunkNumber <= 5 || chunkNumber % 10 == 0)
            {
                ProcessingSummary = $"Audio chunks flowing: {chunkNumber}. Waiting for ASR transcript…";
            }

            await _recitation.SendAudioChunkAsync(chunk);
        }
        catch (Exception ex)
        {
            SetMicFailureState("Mic stream failed. Please start again.");
            _diagnostics.Error("Unhandled error while sending audio chunk.", ex);
        }
    }

    private void OnAudioRecordingError(object? sender, Exception exception)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnAudioRecordingError(sender, exception));
            return;
        }

        IsRecording = false;
        StatusMessage = $"Mic error: {exception.Message}";
        _diagnostics.Error("Audio recording error received in view-model.", exception);
    }

    private void OnMatchResultReceived(object? sender, MatchResult result)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnMatchResultReceived(sender, result));
            return;
        }

        var incomingPrefixLength = ComputeMatchedPrefixLength(result);
        if (!ShouldApplyMatchResult(result, incomingPrefixLength))
        {
            StatusMessage = "Processing speech… keep reciting";
            return;
        }

        _bestSessionResult = CloneMatchResult(result);
        _bestPrefixLength = incomingPrefixLength;
        _bestMatchedWordCount = result.MatchedWordCount;
        _bestProcessedWordCount = result.ProcessedWordCount;
        _bestConfidence = result.Confidence;

        ArabicText = _bestSessionResult.ArabicText;
        Confidence = _bestSessionResult.Confidence;
        Mismatches = _bestSessionResult.Mismatches;
        HighlightedArabicText = BuildHighlightedArabicText(_bestSessionResult);
        ProcessingSummary = BuildProcessingSummary(_bestSessionResult);

        if (_bestSessionResult.Confidence < MatchConfidenceThreshold)
        {
            StatusMessage = "Processing speech… keep reciting";
            return;
        }

        if (_bestSessionResult.Mismatches.Count == 0 && _bestSessionResult.Confidence >= PerfectConfidenceThreshold)
        {
            StatusMessage = $"✓ Surah {_bestSessionResult.SurahNum}:{_bestSessionResult.AyahNum} — perfect!";
            return;
        }

        StatusMessage = _bestSessionResult.Mismatches.Count == 0
            ? $"Surah {_bestSessionResult.SurahNum}:{_bestSessionResult.AyahNum} — match improving ({_bestSessionResult.Confidence:P0})"
            : $"Surah {_bestSessionResult.SurahNum}:{_bestSessionResult.AyahNum} — {_bestSessionResult.Mismatches.Count} correction(s)";
    }

    [RelayCommand]
    private async Task StopSessionAsync()
    {
        await _audio.StopRecordingAsync();
        using (var flushTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
        {
            try
            {
                await _recitation.FlushAsync(flushTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                _diagnostics.Warn("Timed out waiting for final ASR chunk while resetting recitation.");
            }
        }
        await _recitation.DisconnectAsync();
        if (_session.IsAuthenticated)
        {
            await _session.LogoutAsync();
        }

        IsRecording = false;
        ArabicText = string.Empty;
        Confidence = 0;
        Mismatches = [];
        HighlightedArabicText = new FormattedString();
        Interlocked.Exchange(ref _chunkDispatchCount, 0);
        _bestSessionResult = null;
        _bestPrefixLength = 0;
        _bestMatchedWordCount = 0;
        _bestProcessedWordCount = 0;
        _bestConfidence = 0;
        ProcessingSummary = "Session stopped.";
        StatusMessage = "Session reset. Tap Start to recite again.";
        _diagnostics.Info("Local recitation session reset from recitation screen.");
    }

    private void SetMicFailureState(string message)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => SetMicFailureState(message));
            return;
        }

        IsRecording = false;
        StatusMessage = message;
    }

    private async Task LoadPracticeVersePlaceholderAsync()
    {
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var verse = await _verseRepository.GetVerseAsync(1, 1);
            if (verse is null)
            {
                return;
            }

            ArabicText = verse.ArabicText;
            HighlightedArabicText = BuildPendingArabicText(verse.ArabicText);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not preload practice verse text: {ex.Message}");
        }
    }

    private static string BuildProcessingSummary(MatchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.ArabicText))
        {
            return "Audio detected but no recognizable words yet.";
        }

        var totalWords = CountWords(result.ArabicText);
        var processedWords = Math.Min(result.ProcessedWordCount, totalWords);
        var matchedWords = Math.Min(result.MatchedWordCount, processedWords);
        var mismatchCount = result.Mismatches.Count;
        var pendingWords = Math.Max(totalWords - processedWords, 0);

        return $"Processed {processedWords}/{totalWords} words • Matched {matchedWords} • Corrections {mismatchCount} • Pending {pendingWords}";
    }

    private static FormattedString BuildHighlightedArabicText(MatchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.ArabicText))
        {
            return new FormattedString();
        }

        var words = result.ArabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mismatchPositions = result.Mismatches
            .Select(m => m.Position)
            .ToHashSet();
        var processedWords = Math.Min(result.ProcessedWordCount, words.Length);
        var shouldRenderRed = result.Confidence >= MatchConfidenceThreshold;

        var formatted = new FormattedString();
        for (var index = 0; index < words.Length; index++)
        {
            var stateColor = index >= processedWords
                ? Color.FromArgb("#6B7280") // pending
                : mismatchPositions.Contains(index)
                    ? shouldRenderRed
                        ? Color.FromArgb("#E53935") // mismatch
                        : Color.FromArgb("#6B7280") // low-confidence mismatch remains pending
                    : Color.FromArgb("#1A6B3C"); // matched

            formatted.Spans.Add(new Span
            {
                Text = index == words.Length - 1 ? words[index] : $"{words[index]} ",
                TextColor = stateColor,
                FontAttributes = mismatchPositions.Contains(index) ? FontAttributes.Bold : FontAttributes.None
            });
        }

        return formatted;
    }

    private static FormattedString BuildPendingArabicText(string arabicText)
    {
        var formatted = new FormattedString();
        if (string.IsNullOrWhiteSpace(arabicText))
        {
            return formatted;
        }

        var words = arabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < words.Length; index++)
        {
            formatted.Spans.Add(new Span
            {
                Text = index == words.Length - 1 ? words[index] : $"{words[index]} ",
                TextColor = Color.FromArgb("#6B7280")
            });
        }

        return formatted;
    }

    private static int CountWords(string arabicText)
    {
        if (string.IsNullOrWhiteSpace(arabicText))
        {
            return 0;
        }

        return arabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private bool ShouldApplyMatchResult(MatchResult incoming, int incomingPrefixLength)
    {
        if (_bestSessionResult is null)
        {
            return true;
        }

        if (incomingPrefixLength > _bestPrefixLength)
        {
            return true;
        }

        if (incoming.MatchedWordCount > _bestMatchedWordCount)
        {
            return true;
        }

        if (incoming.MatchedWordCount == _bestMatchedWordCount &&
            incoming.ProcessedWordCount > _bestProcessedWordCount &&
            incoming.Confidence >= _bestConfidence - 0.05)
        {
            return true;
        }

        if (incoming.MatchedWordCount == _bestMatchedWordCount &&
            incoming.ProcessedWordCount == _bestProcessedWordCount &&
            incoming.Confidence > _bestConfidence + 0.03)
        {
            return true;
        }

        return incoming.Mismatches.Count == 0 && incoming.Confidence >= PerfectConfidenceThreshold;
    }

    // Preserve the longest correct prefix so early confirmed words stay green.
    private static int ComputeMatchedPrefixLength(MatchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.ArabicText))
        {
            return 0;
        }

        var totalWords = CountWords(result.ArabicText);
        var processed = Math.Min(result.ProcessedWordCount, totalWords);
        if (processed <= 0)
        {
            return 0;
        }

        var mismatches = result.Mismatches
            .Select(m => m.Position)
            .ToHashSet();

        var prefix = 0;
        while (prefix < processed && !mismatches.Contains(prefix))
        {
            prefix++;
        }

        return prefix;
    }

    private static MatchResult CloneMatchResult(MatchResult source)
    {
        return new MatchResult
        {
            SurahNum = source.SurahNum,
            AyahNum = source.AyahNum,
            ArabicText = source.ArabicText,
            Confidence = source.Confidence,
            ProcessedWordCount = source.ProcessedWordCount,
            MatchedWordCount = source.MatchedWordCount,
            Mismatches = source.Mismatches
                .Select(m => new WordMismatch(m.Position, m.Spoken, m.Expected))
                .ToList()
        };
    }
}
