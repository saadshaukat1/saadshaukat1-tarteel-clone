using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelMobile.Models;
using TarteelMobile.Services;
using TarteelMobile.Services.Asr;

namespace TarteelMobile.ViewModels;

public partial class RecitationViewModel : ObservableObject
{
    private const double MatchConfidenceThreshold = 0.65;
    private const double PerfectConfidenceThreshold = 0.90;

    private readonly IAudioService      _audio;
    private readonly IRecitationService _recitation;
    private readonly IVerseRepository _verseRepository;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly IAsrEngine _asrEngine;
    private readonly IServiceProvider _serviceProvider;
    private long _chunkDispatchCount;
    private MatchResult? _bestSessionResult;
    private int _bestPrefixLength;
    private int _bestMatchedWordCount;
    private int _bestProcessedWordCount;
    private double _bestConfidence;

    [ObservableProperty]
    private string _arabicText = string.Empty;

    [ObservableProperty]
    private string _transcriptionText = string.Empty;

    [ObservableProperty]
    private bool _isTranscribing;

    [ObservableProperty]
    private string _statusMessage = "Select a surah and tap the mic to begin";

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

    [ObservableProperty]
    private bool _isVerseVisible = true;

    [ObservableProperty]
    private bool _isVerseCorrect;

    [ObservableProperty]
    private List<TajweedViolation> _tajweedViolations = [];

    [ObservableProperty]
    private bool _hasTajweedViolations;

    [ObservableProperty]
    private bool _isAdvancedMode;

    [ObservableProperty]
    private bool _isFreeReciteMode;

    [ObservableProperty]
    private string _detectedVerseInfo = string.Empty;

    [ObservableProperty]
    private int _selectedSurah = 1;

    [ObservableProperty]
    private int _selectedAyah = 1;

    public List<SurahInfo> Surahs { get; } = [];

    [ObservableProperty]
    private SurahInfo? _selectedSurahInfo;

    [ObservableProperty]
    private List<int> _ayahNumbers = RecitationPracticeSettings.BuildAyahNumbers(1);

    [ObservableProperty]
    private string _asrDebugLog = string.Empty;

    [ObservableProperty]
    private bool _isAsrDebugVisible;

    private readonly Queue<string> _debugLines = new();

    [ObservableProperty]
    private bool _isModelDownloading;

    [ObservableProperty]
    private double _modelDownloadProgress;

    [ObservableProperty]
    private string _modelDownloadStatus = string.Empty;

    [ObservableProperty]
    private bool _isModelDownloadIndeterminate;

    [ObservableProperty]
    private bool _isModelMissing;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _authButtonText = "Log In";

    public bool ShowModelMissingPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsModelMissing);
    public bool ShowModelDownloadingPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsModelDownloading);
    public bool ShowAsrDebugPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsAsrDebugVisible);

    public RecitationViewModel(IAudioService audio,
        IRecitationService recitation,
        IVerseRepository verseRepository,
        ISessionService session,
        IAppDiagnosticsService diagnostics,
        IAsrEngine asrEngine,
        IServiceProvider serviceProvider)
    {
        _audio      = audio;
        _recitation = recitation;
        _verseRepository = verseRepository;
        _session = session;
        _diagnostics = diagnostics;
        _asrEngine = asrEngine;
        _serviceProvider = serviceProvider;

        _audio.AudioChunkReady        += OnAudioChunkReady;
        _audio.RecordingError         += OnAudioRecordingError;
        _recitation.MatchResultReceived += OnMatchResultReceived;
        _recitation.DiagnosticEmitted += OnDiagnosticEmitted;

        RebuildAyahNumbersForSurah(SelectedSurah);
        _ = LoadSurahsAsync();

        if (!_asrEngine.IsReady)
        {
            StartBackgroundInit();
        }

        RefreshAuthState();
    }

    private void StartBackgroundInit()
    {
        IsModelDownloading = true;
        ModelDownloadStatus = "Loading Whisper model…";
        StatusMessage = "Loading Whisper model — please wait…";
        _ = Task.Run(async () =>
        {
            try
            {
                await _asrEngine.InitializeAsync();
            }
            catch (Exception ex)
            {
                _diagnostics.Warn($"Background ASR init failed: {ex.Message}");
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsModelDownloading = false;
                    if (string.IsNullOrWhiteSpace(ModelDownloadStatus) || ModelDownloadStatus.StartsWith("Loading"))
                    {
                        ModelDownloadStatus = string.Empty;
                    }

                    if (_asrEngine.IsReady && !_asrEngine.IsUsingMockMode)
                    {
                        StatusMessage = "Model ready — tap the mic to start reciting.";
                    }
                    else if (_asrEngine.IsUsingMockMode)
                    {
                        StatusMessage = "⚠️ Mock mode — model unavailable. Check debug log.";
                    }
                });
            }
        });
    }

    [RelayCommand]
    private async Task BrowseForModelAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Select Whisper model file (.bin / .gguf)",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI,       new[] { ".bin", ".gguf" } },
                { DevicePlatform.Android,     new[] { "*/*" } },
                { DevicePlatform.iOS,         new[] { "public.data" } },
                { DevicePlatform.MacCatalyst, new[] { "public.data" } },
            })
        };

        var picked = await FilePicker.Default.PickAsync(options);
        if (picked is null)
            return;

        IsModelMissing = false;
        IsModelDownloading = true;
        ModelDownloadStatus = "Copying model from disk…";
        StatusMessage = "Importing Whisper model — please wait…";

        try
        {
            await using var stream = await picked.OpenReadAsync();
            await _asrEngine.ImportModelFromStreamAsync(stream, picked.FullPath is { Length: > 0 }
                ? new FileInfo(picked.FullPath).Exists ? new FileInfo(picked.FullPath).Length : (long?)null
                : null);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Model import failed: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsModelDownloading = false;
                IsModelMissing = true;
                StatusMessage = $"Import failed: {ex.Message}";
            });
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsModelDownloading = false;
            StatusMessage = _asrEngine.IsReady && !_asrEngine.IsUsingMockMode
                ? "Model ready — tap the mic to start reciting."
                : "⚠️ Model could not be loaded after import.";
        });
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (_recitation.RequiresAuthentication && !_session.IsAuthenticated)
        {
            StatusMessage = "Sign in before recording";
            return;
        }

        if (IsModelDownloading)
        {
            StatusMessage = "Wait — Whisper model is downloading";
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
                _recitation.ClearSurahContext();
                _recitation.ClearLastMatchedPosition();
                IsRecording    = false;
                IsTranscribing = false;
                StatusMessage  = "Recitation paused";
                IsAsrDebugVisible = false;
                _diagnostics.Info("Recitation recording paused.");
            }
            else
            {
                Confidence = 0;
                ArabicText = string.Empty;
                TranscriptionText = string.Empty;
                IsTranscribing = false;
                Mismatches = [];
                TajweedViolations = [];
                HasTajweedViolations = false;
                HighlightedArabicText = new FormattedString();
                _debugLines.Clear();
                AsrDebugLog = string.Empty;
                IsAsrDebugVisible = true;
                IsVerseCorrect = false;
                Interlocked.Exchange(ref _chunkDispatchCount, 0);
                _bestSessionResult = null;
                _bestPrefixLength = 0;
                _bestMatchedWordCount = 0;
                _bestProcessedWordCount = 0;
                _bestConfidence = 0;
                ProcessingSummary = "Connecting recitation pipeline…";
                await _recitation.ConnectAsync();

                if (!IsFreeReciteMode)
                {
                    _recitation.SetSurahContext(SelectedSurah, SelectedSurahInfo?.NameArabic ?? string.Empty);
                }

                try
                {
#if ANDROID
                    var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                    if (micStatus != PermissionStatus.Granted)
                    {
                        micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
                        if (micStatus != PermissionStatus.Granted)
                        {
                            StatusMessage = "Microphone permission required to record recitation.";
                            return;
                        }
                    }
#endif
                    await _audio.StartRecordingAsync();
                }
                catch
                {
                    await _recitation.DisconnectAsync();
                    throw;
                }

                if (!IsFreeReciteMode)
                {
                    await LoadPracticeVersePlaceholderAsync();
                }

                IsRecording   = true;
                IsTranscribing = true;
                DetectedVerseInfo = string.Empty;
                StatusMessage = IsFreeReciteMode
                    ? "Listening… recite any verse"
                    : "Listening… recite into the mic";
                ProcessingSummary = "Audio stream active. Transcribing your recitation…";
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
                var summary = $"Chunk #{chunkNumber} sent to Whisper — transcribing…";
                if (MainThread.IsMainThread)
                {
                    ProcessingSummary = summary;
                    IsTranscribing = true;
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ProcessingSummary = summary;
                        IsTranscribing = true;
                    });
                }
            }

            await _recitation.SendAudioChunkAsync(chunk);
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Unhandled error while sending audio chunk.", ex);
            await StopPipelineOnFailureAsync("Mic stream failed. Please start again.");
        }
    }

    private async Task StopPipelineOnFailureAsync(string message)
    {
        try
        {
            await _audio.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not stop audio recording after pipeline failure: {ex.Message}");
        }

        try
        {
            await _recitation.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not disconnect recitation pipeline after failure: {ex.Message}");
        }

        SetMicFailureState(message);
    }

    private void OnAudioRecordingError(object? sender, Exception exception)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnAudioRecordingError(sender, exception));
            return;
        }

        IsRecording = false;
        IsTranscribing = false;
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

        IsTranscribing = false;

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
        TranscriptionText = _bestSessionResult.TranscriptionText;
        Confidence = _bestSessionResult.Confidence;
        Mismatches = _bestSessionResult.Mismatches;
        TajweedViolations = _bestSessionResult.TajweedViolations;
        HasTajweedViolations = _bestSessionResult.TajweedViolations.Count > 0;
        HighlightedArabicText = BuildHighlightedArabicText(_bestSessionResult);
        ProcessingSummary = BuildProcessingSummary(_bestSessionResult);
        IsVerseCorrect = _bestSessionResult.Mismatches.Count == 0
                         && _bestSessionResult.Confidence >= PerfectConfidenceThreshold;

        if (_bestSessionResult.Confidence < MatchConfidenceThreshold)
        {
            StatusMessage = "Processing speech… keep reciting";
            return;
        }

        var surahNum = _bestSessionResult.SurahNum;
        var ayahNum  = _bestSessionResult.AyahNum;

        if (IsFreeReciteMode && surahNum > 0)
        {
            var surahName = _bestSessionResult.SurahNameEnglish ?? $"Surah {surahNum}";
            DetectedVerseInfo = $"{surahName} — {surahNum}:{ayahNum}";
            if (string.IsNullOrEmpty(ArabicText) || ArabicText != _bestSessionResult.ArabicText)
            {
                ArabicText = _bestSessionResult.ArabicText;
                HighlightedArabicText = BuildHighlightedArabicText(_bestSessionResult);
            }
        }

        if (_bestSessionResult.Mismatches.Count == 0 && _bestSessionResult.Confidence >= PerfectConfidenceThreshold)
        {
            StatusMessage = $"✓ Surah {surahNum}:{ayahNum} — perfect";
            return;
        }

        StatusMessage = _bestSessionResult.Mismatches.Count == 0
            ? $"Surah {surahNum}:{ayahNum} — match improving ({_bestSessionResult.Confidence:P0})"
            : $"Surah {surahNum}:{ayahNum} — {_bestSessionResult.Mismatches.Count} correction(s)";
    }

    [RelayCommand]
    private async Task StopSessionAsync()
    {
        if (IsRecording)
        {
            var confirmed = await Shell.Current.DisplayAlertAsync(
                "Reset session",
                "Stop recording and clear the current recitation? Your progress for this session will be lost.",
                "Reset", "Cancel");
            if (!confirmed)
                return;
        }

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
        _recitation.ClearLastMatchedPosition();
        _recitation.ClearSurahContext();
        await _recitation.DisconnectAsync();

        IsRecording = false;
        IsTranscribing = false;
        ArabicText = string.Empty;
        TranscriptionText = string.Empty;
        Confidence = 0;
        Mismatches = [];
        TajweedViolations = [];
        HasTajweedViolations = false;
        HighlightedArabicText = new FormattedString();
        IsAsrDebugVisible = false;
        _debugLines.Clear();
        AsrDebugLog = string.Empty;
        IsVerseCorrect = false;
        Interlocked.Exchange(ref _chunkDispatchCount, 0);
        _bestSessionResult = null;
        _bestPrefixLength = 0;
        _bestMatchedWordCount = 0;
        _bestProcessedWordCount = 0;
        _bestConfidence = 0;
        ProcessingSummary = "Session stopped.";
        StatusMessage = IsFreeReciteMode
            ? "Session reset. Tap the mic to recite any verse."
            : "Session reset. Select a surah and tap the mic to begin.";
        DetectedVerseInfo = string.Empty;
        _diagnostics.Info("Local recitation session reset from recitation screen.");
    }

    [RelayCommand]
    private void ToggleVerseVisibility()
    {
        IsVerseVisible = !IsVerseVisible;
    }

    [RelayCommand]
    private void ToggleAdvancedMode()
    {
        IsAdvancedMode = !IsAdvancedMode;
    }

    partial void OnSelectedSurahInfoChanged(SurahInfo? value)
    {
        if (value is null) return;
        SelectedSurah = value.SurahNum;
        RebuildAyahNumbersForSurah(value.SurahNum);
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelMissingPanel));
        OnPropertyChanged(nameof(ShowModelDownloadingPanel));
        OnPropertyChanged(nameof(ShowAsrDebugPanel));
    }

    partial void OnIsModelMissingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelMissingPanel));
    }

    partial void OnIsModelDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelDownloadingPanel));
    }

    partial void OnIsAsrDebugVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAsrDebugPanel));
    }

    private async Task LoadSurahsAsync()
    {
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var surahs = await _verseRepository.GetAllSurahsAsync();
            Surahs.Clear();
            Surahs.AddRange(surahs);
            OnPropertyChanged(nameof(Surahs));
            SelectedSurahInfo = Surahs.FirstOrDefault(s => s.SurahNum == 1);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Failed to load surah list: {ex.Message}");
        }
    }

    private void RebuildAyahNumbersForSurah(int surah)
    {
        AyahNumbers = RecitationPracticeSettings.BuildAyahNumbers(surah);
        SelectedAyah = RecitationPracticeSettings.ClampAyah(SelectedAyah, surah);
    }

    private void SetMicFailureState(string message)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => SetMicFailureState(message));
            return;
        }

        IsRecording = false;
        IsTranscribing = false;
        StatusMessage = message;
    }

    private void OnDiagnosticEmitted(object? sender, string message)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnDiagnosticEmitted(sender, message));
            return;
        }

        _debugLines.Enqueue(message);
        while (_debugLines.Count > 8)
        {
            _debugLines.Dequeue();
        }

        AsrDebugLog = string.Join(Environment.NewLine, _debugLines);
    }

    private async Task LoadPracticeVersePlaceholderAsync()
    {
        await LoadVerseAsync(SelectedSurah, SelectedAyah);
    }

    [RelayCommand]
    private async Task LoadSelectedVerseAsync()
    {
        _recitation.ClearLastMatchedPosition();
        await LoadVerseAsync(SelectedSurah, SelectedAyah);
    }

    private async Task LoadVerseAsync(int surah, int ayah)
    {
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var verse = await _verseRepository.GetVerseAsync(surah, ayah);
            if (verse is null)
            {
                return;
            }

            ArabicText = verse.ArabicText;
            HighlightedArabicText = BuildPendingArabicText(verse.ArabicText);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not load verse {surah}:{ayah} — {ex.Message}");
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
            var isPending = index >= processedWords;
            var isMismatch = mismatchPositions.Contains(index);
            var isMatched = !isPending && !isMismatch;

            var stateColor = isPending
                ? Color.FromArgb("#6B7280")
                : isMismatch
                    ? shouldRenderRed
                        ? Color.FromArgb("#E53935")
                        : Color.FromArgb("#6B7280")
                    : Color.FromArgb("#1A6B3C");

            formatted.Spans.Add(new Span
            {
                Text = $"{words[index]}{(index == words.Length - 1 ? string.Empty : " ")}",
                TextColor = stateColor,
                FontAttributes = isMismatch ? FontAttributes.Bold : FontAttributes.None,
                AutomationId = isMatched ? $"word-matched-{index}"
                    : isMismatch ? $"word-correction-{index}"
                    : $"word-pending-{index}"
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
            TranscriptionText = source.TranscriptionText,
            Confidence = source.Confidence,
            ProcessedWordCount = source.ProcessedWordCount,
            MatchedWordCount = source.MatchedWordCount,
            Mismatches = source.Mismatches
                .Select(m => new WordMismatch(m.Position, m.Spoken, m.Expected))
                .ToList()
        };
    }

    [RelayCommand]
    private async Task ToggleAuthAsync()
    {
        if (IsAuthenticated)
        {
            await _session.LogoutAsync();
            IsAuthenticated = false;
            UserEmail = string.Empty;
            AuthButtonText = "Log In";
            StatusMessage = "Signed out. Log in to save your progress.";
        }
        else
        {
            var loginPage = _serviceProvider.GetRequiredService<Views.LoginPage>();
            await Shell.Current.Navigation.PushModalAsync(loginPage);
        }
    }

    public void RefreshAuthState()
    {
        IsAuthenticated = _session.IsAuthenticated;
        UserEmail = _session.CurrentUserEmail ?? string.Empty;
        AuthButtonText = IsAuthenticated ? "Log Out" : "Log In";
    }
}
