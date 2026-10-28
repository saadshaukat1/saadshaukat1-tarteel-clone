using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelMobile.Models;
using TarteelMobile.Services;
using TarteelMobile.Services.Asr;
using TarteelMobile.Views;
using TarteelClone.LocalRecitationCore.Services;
using MadrasaRecitationMode = TarteelClone.LocalRecitationCore.Models.MadrasaRecitationMode;
using MadrasaGrade = TarteelClone.LocalRecitationCore.Models.MadrasaGrade;
using MadrasaTeacherDecision = TarteelClone.LocalRecitationCore.Models.MadrasaTeacherDecision;
using MadrasaEvaluation = TarteelClone.LocalRecitationCore.Models.MadrasaEvaluation;
using MadrasaEvaluationInput = TarteelClone.LocalRecitationCore.Models.MadrasaEvaluationInput;
using MutashabihaChallenge = TarteelClone.LocalRecitationCore.Models.MutashabihaChallenge;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.ViewModels;

public partial class RecitationViewModel : ObservableObject, IQueryAttributable
{
    private const double MatchConfidenceThreshold = 0.65;
    private const double PerfectConfidenceThreshold = 0.90;
    private const double PartialAttemptSaveThreshold = 0.30;

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
    private readonly ITodayWorkflowService _today;
    private long? _assignmentId;
    private long? _sessionId;
    private bool _attemptSaved;
    private string _assignmentReason = string.Empty;

    private readonly QuranAlignmentEngine _alignmentEngine = new();
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();
    private IDispatcherTimer? _timer;
    private readonly IMadrasaAudioFeedbackService _madrasaAudio;
    private DateTime _lastTokenTimeUtc = DateTime.UtcNow;

    public MushafCanvasDrawable CanvasDrawable { get; } = new();
    public List<QuranWordModel> PageWords { get; set; } = [];
    public List<AyahMarkerModel> AyahMarkers { get; set; } = [];

    private int _currentWordCursor;
    public int CurrentWordCursor { get => _currentWordCursor; set => SetProperty(ref _currentWordCursor, value); }

    private int _mistakesCount;
    public int MistakesCount { get => _mistakesCount; set => SetProperty(ref _mistakesCount, value); }

    private double _accuracy = 100.0;
    public double Accuracy { get => _accuracy; set => SetProperty(ref _accuracy, value); }

    private string _elapsedTimeString = "00:00";
    public string ElapsedTimeString { get => _elapsedTimeString; set => SetProperty(ref _elapsedTimeString, value); }

    private string _liveTokenText = string.Empty;
    public string LiveTokenText { get => _liveTokenText; set => SetProperty(ref _liveTokenText, value); }

    private bool _hasLiveToken;
    public bool HasLiveToken { get => _hasLiveToken; set => SetProperty(ref _hasLiveToken, value); }

    public string FabIconText => IsRecording ? "⏹" : "🎙";
    public Color MicButtonColor => IsRecording ? Color.FromArgb("#EF4444") : Color.FromArgb("#10B981");
    public Color MicBorderColor => IsRecording ? Color.FromArgb("#F87171") : Color.FromArgb("#059669");
    public Color BlindModeButtonColor => IsBlindRecitationMode ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
    public Color TajweedButtonColor => IsTajweedMode ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
    public Color AutoDetectButtonColor => IsFreeReciteMode ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");

    public Color StatusBannerBackgroundColor => IsRecording
        ? Color.FromArgb("#7F1D1D")
        : IsTranscribing
            ? Color.FromArgb("#0C4A6E")
            : Color.FromArgb("#1E232B");

    public Color StatusBannerTextColor => IsRecording
        ? Color.FromArgb("#FEE2E2")
        : IsTranscribing
            ? Color.FromArgb("#BAE6FD")
            : Color.FromArgb("#CBD5E1");

    public string StatusBannerIcon => IsRecording
        ? "🔴"
        : IsTranscribing
            ? "⚡"
            : "🎙";

    private bool _isSurahPickerOpen;
    public bool IsSurahPickerOpen { get => _isSurahPickerOpen; set => SetProperty(ref _isSurahPickerOpen, value); }

    private string _surahSearchText = string.Empty;
    public string SurahSearchText
    {
        get => _surahSearchText;
        set
        {
            if (SetProperty(ref _surahSearchText, value))
            {
                OnPropertyChanged(nameof(HasSearchText));
                FilterSurahs();
            }
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SurahSearchText);

    private string _surahCategoryFilter = "All";
    public string SurahCategoryFilter
    {
        get => _surahCategoryFilter;
        set
        {
            if (SetProperty(ref _surahCategoryFilter, value))
            {
                FilterSurahs();
            }
        }
    }

    public ObservableCollection<SurahInfo> FilteredSurahs { get; } = [];

    private bool _isBlindRecitationMode = true;
    public bool IsBlindRecitationMode
    {
        get => _isBlindRecitationMode;
        set
        {
            if (SetProperty(ref _isBlindRecitationMode, value))
            {
                CanvasDrawable.IsBlindRecitationMode = value;
                RequestCanvasRedraw?.Invoke();
            }
        }
    }

    private bool _isTajweedMode = true;
    public bool IsTajweedMode
    {
        get => _isTajweedMode;
        set
        {
            if (SetProperty(ref _isTajweedMode, value))
            {
                CanvasDrawable.IsTajweedColored = value;
                RequestCanvasRedraw?.Invoke();
            }
        }
    }

    private string _headerTitle = "Al-Mulk";
    public string HeaderTitle { get => _headerTitle; set => SetProperty(ref _headerTitle, value); }

    private string _headerSubtitle = "Page 562 | Juz 29 | Hizb 57 | 67:3";
    public string HeaderSubtitle { get => _headerSubtitle; set => SetProperty(ref _headerSubtitle, value); }

    private string _surahBannerArabic = "سُورَةُ الْمُلْكِ";
    public string SurahBannerArabic { get => _surahBannerArabic; set => SetProperty(ref _surahBannerArabic, value); }

    private string _bismillahText = "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ";
    public string BismillahText { get => _bismillahText; set => SetProperty(ref _bismillahText, value); }

    public event Action? RequestCanvasRedraw;
    private float _lastCanvasWidth = 380f;
    private float _lastCanvasHeight = 560f;

    private string _arabicText = string.Empty;
    public string ArabicText { get => _arabicText; set => SetProperty(ref _arabicText, value); }
    private string _transcriptionText = string.Empty;
    public string TranscriptionText { get => _transcriptionText; set => SetProperty(ref _transcriptionText, value); }
    private bool _isTranscribing;
    public bool IsTranscribing { get => _isTranscribing; set => SetProperty(ref _isTranscribing, value); }
    private string _statusMessage = "Select a surah and tap the mic to begin";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    private bool _isRecording;
    public bool IsRecording { get => _isRecording; set { if (SetProperty(ref _isRecording, value)) { OnPropertyChanged(nameof(MicButtonColor)); OnPropertyChanged(nameof(FabIconText)); } } }
    private bool _isResetting;
    public bool IsResetting { get => _isResetting; set => SetProperty(ref _isResetting, value); }
    private double _confidence;
    public double Confidence { get => _confidence; set { if (SetProperty(ref _confidence, value)) { OnPropertyChanged(nameof(ConfidenceBarColor)); } } }
    private List<WordMismatch> _mismatches = [];
    public List<WordMismatch> Mismatches { get => _mismatches; set { if (SetProperty(ref _mismatches, value)) { OnPropertyChanged(nameof(HasMismatches)); } } }
    private FormattedString _highlightedArabicText = new();
    public FormattedString HighlightedArabicText { get => _highlightedArabicText; set => SetProperty(ref _highlightedArabicText, value); }
    private string _processingSummary = "Awaiting audio processing…";
    public string ProcessingSummary { get => _processingSummary; set => SetProperty(ref _processingSummary, value); }
    public string LiveMatchSummary => _bestSessionResult is null
        ? "Listening"
        : $"{_bestMatchedWordCount}/{_bestProcessedWordCount}";
    private bool _isVerseVisible = true;
    public bool IsVerseVisible { get => _isVerseVisible; set => SetProperty(ref _isVerseVisible, value); }
    private bool _isVerseCorrect;
    public bool IsVerseCorrect { get => _isVerseCorrect; set => SetProperty(ref _isVerseCorrect, value); }
    private List<TajweedViolation> _tajweedViolations = [];
    public List<TajweedViolation> TajweedViolations { get => _tajweedViolations; set => SetProperty(ref _tajweedViolations, value); }
    private bool _hasTajweedViolations;
    public bool HasTajweedViolations { get => _hasTajweedViolations; set => SetProperty(ref _hasTajweedViolations, value); }
    private bool _isAdvancedMode;
    public bool IsAdvancedMode { get => _isAdvancedMode; set => SetProperty(ref _isAdvancedMode, value); }
    private bool _isFreeReciteMode;
    public bool IsFreeReciteMode { get => _isFreeReciteMode; set => SetProperty(ref _isFreeReciteMode, value); }
    private string _detectedVerseInfo = string.Empty;
    public string DetectedVerseInfo { get => _detectedVerseInfo; set => SetProperty(ref _detectedVerseInfo, value); }
    private int _selectedSurah = 67;
    public int SelectedSurah { get => _selectedSurah; set => SetProperty(ref _selectedSurah, value); }
    private int _selectedAyah = 1;
    public int SelectedAyah { get => _selectedAyah; set => SetProperty(ref _selectedAyah, value); }

    public ObservableCollection<SurahInfo> Surahs { get; } = [];

    private SurahInfo? _selectedSurahInfo;
    public SurahInfo? SelectedSurahInfo { get => _selectedSurahInfo; set { if (SetProperty(ref _selectedSurahInfo, value)) OnSelectedSurahInfoChanged(value); } }
    private List<int> _ayahNumbers = RecitationPracticeSettings.BuildAyahNumbers(67);
    public List<int> AyahNumbers { get => _ayahNumbers; set => SetProperty(ref _ayahNumbers, value); }
    private string _asrDebugLog = string.Empty;
    public string AsrDebugLog { get => _asrDebugLog; set => SetProperty(ref _asrDebugLog, value); }
    private bool _isAsrDebugVisible;
    public bool IsAsrDebugVisible { get => _isAsrDebugVisible; set { if (SetProperty(ref _isAsrDebugVisible, value)) OnIsAsrDebugVisibleChanged(value); } }

    private bool _isTogglingRecording;
    private DateTimeOffset _lastToggleTimeUtc = DateTimeOffset.MinValue;

    private readonly Queue<string> _debugLines = new();

    private bool _isModelDownloading;
    public bool IsModelDownloading { get => _isModelDownloading; set { if (SetProperty(ref _isModelDownloading, value)) OnIsModelDownloadingChanged(value); } }
    private double _modelDownloadProgress;
    public double ModelDownloadProgress { get => _modelDownloadProgress; set => SetProperty(ref _modelDownloadProgress, value); }
    private string _modelDownloadStatus = string.Empty;
    public string ModelDownloadStatus { get => _modelDownloadStatus; set => SetProperty(ref _modelDownloadStatus, value); }
    private bool _isModelDownloadIndeterminate;
    public bool IsModelDownloadIndeterminate { get => _isModelDownloadIndeterminate; set => SetProperty(ref _isModelDownloadIndeterminate, value); }
    private bool _isModelMissing;
    public bool IsModelMissing { get => _isModelMissing; set { if (SetProperty(ref _isModelMissing, value)) OnIsModelMissingChanged(value); } }
    private bool _isAuthenticated;
    public bool IsAuthenticated { get => _isAuthenticated; set => SetProperty(ref _isAuthenticated, value); }
    private string _userEmail = string.Empty;
    public string UserEmail { get => _userEmail; set => SetProperty(ref _userEmail, value); }
    private string _authButtonText = "Log In";
    public string AuthButtonText { get => _authButtonText; set => SetProperty(ref _authButtonText, value); }
    public bool IsGuidedAssignment => _assignmentId is not null;
    public string AssignmentReason => _assignmentReason;
    public bool HasNextAssignment => IsGuidedAssignment;
    public string CurrentAyahLabel => $"{SelectedSurah}:{SelectedAyah}";

    public bool HasMismatches => Mismatches.Count > 0;
    public Color ConfidenceBarColor => Confidence >= PerfectConfidenceThreshold
        ? Color.FromArgb("#1A6B3C")
        : Confidence >= MatchConfidenceThreshold
            ? Color.FromArgb("#C58A2C")
            : Color.FromArgb("#6B7280");

    public bool ShowModelMissingPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsModelMissing);
    public bool ShowModelDownloadingPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsModelDownloading);
    public bool ShowAsrDebugPanel => RecitationPracticeSettings.ShouldShowAdvancedPanel(IsAdvancedMode, IsAsrDebugVisible);

    public RecitationViewModel(IAudioService audio,
        IRecitationService recitation,
        IVerseRepository verseRepository,
        ISessionService session,
        IAppDiagnosticsService diagnostics,
        IAsrEngine asrEngine,
        IServiceProvider serviceProvider,
        ITodayWorkflowService today,
        IMadrasaAudioFeedbackService? madrasaAudio = null)
    {
        _audio      = audio;
        _recitation = recitation;
        _verseRepository = verseRepository;
        _session = session;
        _diagnostics = diagnostics;
        _asrEngine = asrEngine;
        _serviceProvider = serviceProvider;
        _today = today;
        _madrasaAudio = madrasaAudio ?? new MadrasaAudioFeedbackService(diagnostics);

        _audio.AudioChunkReady        += OnAudioChunkReady;
        _audio.RecordingError         += OnAudioRecordingError;
        _recitation.MatchResultReceived += OnMatchResultReceived;
        _recitation.DiagnosticEmitted += OnDiagnosticEmitted;
        _asrEngine.DownloadProgressChanged += OnAsrDownloadProgress;

        _timer = Application.Current?.Dispatcher?.CreateTimer();
        if (_timer is not null)
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                ElapsedTimeString = _stopwatch.Elapsed.ToString(@"mm\:ss");
                if (IsRecording && (DateTime.UtcNow - _lastTokenTimeUtc).TotalSeconds > 5.0 && CurrentWordCursor < PageWords.Count)
                {
                    HesitationCount++;
                    StatusMessage = "الشيخ يستمع لتلاوتك... اضغط «لقمة» إذا احتجت مساعدة";
                    _lastTokenTimeUtc = DateTime.UtcNow;
                }
            };
        }

        InitializePageLayout(380f, 560f);

        RebuildAyahNumbersForSurah(SelectedSurah);
        _ = LoadSurahsAsync();

        if (!_asrEngine.IsReady)
        {
            StartBackgroundInit();
        }

        RefreshAuthState();
    }

    public void InitializePageLayout(float width, float height)
    {
        if (width > 50) _lastCanvasWidth = width;
        if (height > 50) _lastCanvasHeight = height;

        var (words, markers) = MushafLayoutService.BuildPage562Layout(_lastCanvasWidth, _lastCanvasHeight);
        PageWords = words;
        AyahMarkers = markers;
        CanvasDrawable.Words = PageWords;
        CanvasDrawable.Markers = AyahMarkers;
        CanvasDrawable.IsBlindRecitationMode = IsBlindRecitationMode;
        CanvasDrawable.IsTajweedColored = IsTajweedMode;

        RequestCanvasRedraw?.Invoke();
    }

    public void ProcessSpokenText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || PageWords.Count == 0) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tokens = ArabicTextNormalizer.TokenizeAndNormalize(text);
            foreach (var token in tokens)
            {
                if (CurrentWordCursor >= PageWords.Count)
                    break;

                LiveTokenText = token;
                HasLiveToken = true;

                _lastTokenTimeUtc = DateTime.UtcNow;
                var match = _alignmentEngine.AlignToken(token, PageWords, CurrentWordCursor);
                switch (match.Outcome)
                {
                    case AlignmentOutcome.ExactMatch:
                        PageWords[match.MatchedIndex].Status = WordRecitationStatus.Revealed;
                        CurrentWordCursor = match.MatchedIndex + 1;
                        break;

                    case AlignmentOutcome.ForwardGap:
                        foreach (var skippedIdx in match.SkippedIndices)
                        {
                            PageWords[skippedIdx].Status = WordRecitationStatus.Skipped;
                            MistakesCount++;
                        }
                        PageWords[match.MatchedIndex].Status = WordRecitationStatus.Revealed;
                        CurrentWordCursor = match.MatchedIndex + 1;
                        RecalculateAccuracy();
                        _ = _madrasaAudio.PlayTeacherKnockAlertAsync();
                        var skippedWord = PageWords[match.SkippedIndices[0]].UthmaniText;
                        StatusMessage = $"تنبيه الشيخ: أعد الكلمة «{skippedWord}»!";
                        break;

                    case AlignmentOutcome.BacktrackCorrection:
                        PageWords[match.MatchedIndex].Status = WordRecitationStatus.Corrected;
                        StatusMessage = $"أحسنت! تم تصحيح كلمة «{PageWords[match.MatchedIndex].UthmaniText}»";
                        break;
                }
            }

            if (CurrentWordCursor < PageWords.Count)
            {
                var curAyah = PageWords[CurrentWordCursor].AyahNumber;
                HeaderSubtitle = $"Page 562 | Juz 29 | Hizb 57 | 67:{curAyah}";
            }
            else if (CurrentWordCursor >= PageWords.Count && PageWords.Count > 0 && IsRecording)
            {
                _ = CompleteRecitationSessionAsync();
            }

            RequestCanvasRedraw?.Invoke();
        });
    }

    private void RecalculateAccuracy()
    {
        int totalExpected = Math.Max(1, CurrentWordCursor);
        Accuracy = Math.Max(0.0, ((totalExpected - MistakesCount) / (double)totalExpected) * 100.0);
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("assignmentId", out var rawAssignmentId) ||
            !long.TryParse(rawAssignmentId?.ToString(), out var assignmentId))
        {
            return;
        }

        _assignmentId = assignmentId;
        _assignmentReason = query.TryGetValue("reason", out var reason) ? reason?.ToString() ?? string.Empty : string.Empty;
        if (query.TryGetValue("surah", out var rawSurah) && int.TryParse(rawSurah?.ToString(), out var surah))
        {
            SelectedSurah = surah;
        }

        if (query.TryGetValue("ayah", out var rawAyah) && int.TryParse(rawAyah?.ToString(), out var ayah))
        {
            SelectedAyah = ayah;
        }

        OnPropertyChanged(nameof(IsGuidedAssignment));
        OnPropertyChanged(nameof(CurrentAyahLabel));
        OnPropertyChanged(nameof(AssignmentReason));
        OnPropertyChanged(nameof(HasNextAssignment));
        StatusMessage = string.IsNullOrWhiteSpace(_assignmentReason)
            ? "Assignment ready — tap the mic to begin."
            : $"{_assignmentReason} — tap the mic to begin.";
        _ = LoadVerseAsync(SelectedSurah, SelectedAyah);
    }

    [RelayCommand]
    private void Retry()
    {
        if (IsRecording)
        {
            return;
        }

        ResetCurrentResult();
        StatusMessage = "Ready to retry this assignment.";
    }

    [RelayCommand]
    private async Task NextAssignmentAsync()
    {
        if (!IsGuidedAssignment || IsRecording)
        {
            return;
        }

        var next = (await _today.GetTodayAssignmentsAsync(_session.CurrentUserEmail))
            .FirstOrDefault(item => item.Assignment.Id != _assignmentId && item.Assignment.Status != CoreModels.LessonAssignmentStatus.Completed);
        if (next is null)
        {
            StatusMessage = "Today is complete. Return to Progress for the next review.";
            return;
        }

        ApplyQueryAttributes(new Dictionary<string, object>
        {
            ["assignmentId"] = next.Assignment.Id,
            ["surah"] = next.Assignment.SurahNum,
            ["ayah"] = next.Assignment.AyahNum,
            ["reason"] = next.ReasonDisplay
        });
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (_isTogglingRecording)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastToggleTimeUtc).TotalMilliseconds < 450)
        {
            return;
        }
        _lastToggleTimeUtc = now;
        _isTogglingRecording = true;

        try
        {
            if (_recitation.RequiresAuthentication && !_session.IsAuthenticated)
            {
                StatusMessage = "Sign in before recording";
                NotifyUiStateChanged();
                return;
            }

            if (!_asrEngine.IsReady)
            {
                StatusMessage = "Loading Whisper model… please wait";
                NotifyUiStateChanged();
                try
                {
                    await _asrEngine.InitializeAsync();
                }
                catch (Exception ex)
                {
                    SetMicFailureState($"ASR init failed: {ex.Message}");
                    return;
                }
            }

            if (IsRecording)
            {
                _stopwatch.Stop();
                _timer?.Stop();
                HasLiveToken = false;
                IsRecording    = false;
                IsTranscribing = false;
                StatusMessage  = $"Recitation paused • Tap mic to resume {HeaderTitle}";
                IsAsrDebugVisible = false;
                NotifyUiStateChanged();
                _diagnostics.Info("Recitation recording paused.");

                try
                {
                    await _audio.StopRecordingAsync();
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn($"Audio stop issue: {ex.Message}");
                }

                try
                {
                    await _recitation.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn($"Pipeline disconnect issue: {ex.Message}");
                }

                _recitation.ClearSurahContext();
                _recitation.ClearLastMatchedPosition();
                await SaveBestPartialAttemptAsync(CoreModels.RecitationSessionStatus.Abandoned);
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

                StatusMessage = $"Connecting mic for Surah {HeaderTitle}…";
                ProcessingSummary = "Connecting recitation pipeline…";
                NotifyUiStateChanged();

                if (IsGuidedAssignment)
                {
                    if (_sessionId is not null)
                    {
                        await SaveBestPartialAttemptAsync(CoreModels.RecitationSessionStatus.Abandoned);
                        try
                        {
                            await _verseRepository.CloseRecitationSessionAsync(_sessionId.Value, CoreModels.RecitationSessionStatus.Abandoned);
                        }
                        catch (Exception ex)
                        {
                            _diagnostics.Warn($"Could not close previous session before retry: {ex.Message}");
                        }
                    }

                    var session = await _verseRepository.OpenRecitationSessionAsync(_session.CurrentUserEmail);
                    _sessionId = session.Id;
                    await _verseRepository.MarkAssignmentInProgressAsync(_assignmentId!.Value, session.Id, _session.CurrentUserEmail);
                }

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
                            await _recitation.DisconnectAsync();
                            NotifyUiStateChanged();
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

                _stopwatch.Restart();
                _timer?.Start();
                IsRecording   = true;
                IsTranscribing = false;
                DetectedVerseInfo = string.Empty;
                StatusMessage = IsFreeReciteMode
                    ? "🔴 Listening… recite any verse"
                    : $"🔴 Listening… recite Surah {HeaderTitle}";
                ProcessingSummary = "Audio stream active. Transcribing your recitation…";
                NotifyUiStateChanged();
                _diagnostics.Info($"Recitation recording started for Surah {SelectedSurah}.");
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
        finally
        {
            _isTogglingRecording = false;
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

    private async void OnMatchResultReceived(object? sender, MatchResult result)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => OnMatchResultReceived(sender, result));
            return;
        }

        IsTranscribing = false;

        if (!string.IsNullOrWhiteSpace(result.TranscriptionText))
        {
            ProcessSpokenText(result.TranscriptionText);
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
        OnPropertyChanged(nameof(LiveMatchSummary));

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

        if (surahNum > 0 && surahNum != SelectedSurah && (IsFreeReciteMode || _bestSessionResult.Confidence >= MatchConfidenceThreshold))
        {
            var surahName = _bestSessionResult.SurahNameEnglish ?? $"Surah {surahNum}";
            DetectedVerseInfo = $"{surahName} — {surahNum}:{ayahNum}";
            StatusMessage = $"Auto-detected: {surahName} ({surahNum}:{ayahNum})";
            _ = LoadSurahAsync(surahNum, ayahNum);
        }
        else if (IsFreeReciteMode && surahNum > 0)
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
            StatusMessage = $"Surah {surahNum}:{ayahNum} matched. Listening for the next ayah.";
            await SaveGuidedAttemptAsync(_bestSessionResult);
            if (!IsGuidedAssignment && !IsFreeReciteMode
                && surahNum == SelectedSurah && ayahNum == SelectedAyah)
            {
                await AdvanceToNextAyahAsync(surahNum, ayahNum);
            }
            return;
        }

        if (_bestSessionResult.Confidence >= MatchConfidenceThreshold)
        {
            await SaveGuidedAttemptAsync(_bestSessionResult);
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
            var confirmed = await Shell.Current.DisplayAlert(
                "Reset session",
                "Stop recording and clear the current recitation? Your progress for this session will be lost.",
                "Reset", "Cancel");
            if (!confirmed)
                return;
        }

        IsResetting = true;
        try
        {
            try
            {
                await _audio.StopRecordingAsync();
            }
            catch (Exception ex)
            {
                _diagnostics.Warn($"Could not stop audio while resetting recitation: {ex.Message}");
            }

            try
            {
                await _recitation.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _diagnostics.Warn($"Could not disconnect recitation pipeline while resetting: {ex.Message}");
            }

            _recitation.ClearLastMatchedPosition();
            _recitation.ClearSurahContext();

            if (_sessionId is not null)
            {
                try
                {
                    await SaveBestPartialAttemptAsync(CoreModels.RecitationSessionStatus.Abandoned);
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn($"Could not save partial attempt while resetting: {ex.Message}");
                }

                try
                {
                    await _verseRepository.CloseRecitationSessionAsync(_sessionId.Value, CoreModels.RecitationSessionStatus.Abandoned);
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn($"Could not close guided session while resetting: {ex.Message}");
                }

                _sessionId = null;
            }

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
        finally
        {
            IsRecording = false;
            IsTranscribing = false;
            IsResetting = false;
        }
    }

    [RelayCommand]
    private void ToggleVerseVisibility()
    {
        IsVerseVisible = !IsVerseVisible;
    }

    [RelayCommand]
    private async Task OpenInMushafAsync()
    {
        // Prefer the last matched verse; otherwise the currently selected verse.
        var surahNum = _bestSessionResult?.SurahNum > 0 ? _bestSessionResult.SurahNum : SelectedSurah;
        var ayahNum = _bestSessionResult?.AyahNum > 0 ? _bestSessionResult.AyahNum : SelectedAyah;

        try
        {
            await Shell.Current.GoToAsync("//MushafPageView");
            if (_serviceProvider.GetService(typeof(MushafPageViewModel)) is MushafPageViewModel mushafVm)
            {
                await mushafVm.ShowVerseAsync(surahNum, ayahNum);
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not open Mushaf view: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleAdvancedMode()
    {
        IsAdvancedMode = !IsAdvancedMode;
    }

    private void OnSelectedSurahInfoChanged(SurahInfo? value)
    {
        if (value is null) return;
        SelectedSurah = value.SurahNum;
        RebuildAyahNumbersForSurah(value.SurahNum);
        OnPropertyChanged(nameof(CurrentAyahLabel));
    }

    private void OnIsAdvancedModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelMissingPanel));
        OnPropertyChanged(nameof(ShowModelDownloadingPanel));
        OnPropertyChanged(nameof(ShowAsrDebugPanel));
    }

    private void OnIsModelMissingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelMissingPanel));
    }

    private void OnIsModelDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowModelDownloadingPanel));
    }

    private void OnIsAsrDebugVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAsrDebugPanel));
    }

    private async Task LoadSurahsAsync()
    {
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var surahs = await _verseRepository.GetAllSurahsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Surahs.Clear();
                foreach (var surah in surahs)
                {
                    Surahs.Add(surah);
                }

                SelectedSurahInfo = Surahs.FirstOrDefault(s => s.SurahNum == SelectedSurah);
                if (SelectedSurahInfo is null && Surahs.Count > 0)
                {
                    SelectedSurahInfo = Surahs.FirstOrDefault(s => s.SurahNum == 67) ?? Surahs[0];
                    SelectedSurah = SelectedSurahInfo.SurahNum;
                }

                FilterSurahs();
            });

            if (Surahs.Count == 0)
            {
                StatusMessage = "No surah data available. Check the local Quran data.";
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Failed to load surah list: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                StatusMessage = "Could not load surah list. Check the local Quran data.");
        }
    }

    private void FilterSurahs()
    {
        FilteredSurahs.Clear();
        var query = (SurahSearchText ?? string.Empty).Trim().ToLowerInvariant();

        IEnumerable<SurahInfo> items = Surahs;

        if (SurahCategoryFilter == "Popular")
        {
            int[] popular = [1, 18, 36, 55, 56, 67, 112, 113, 114];
            items = items.Where(s => popular.Contains(s.SurahNum));
        }
        else if (SurahCategoryFilter == "Juz30")
        {
            items = items.Where(s => s.SurahNum >= 78);
        }
        else if (SurahCategoryFilter == "Meccan")
        {
            items = items.Where(s => s.RevelationType.Contains("Meccan", StringComparison.OrdinalIgnoreCase) || s.RevelationType.Contains("Makki", StringComparison.OrdinalIgnoreCase));
        }
        else if (SurahCategoryFilter == "Medinan")
        {
            items = items.Where(s => s.RevelationType.Contains("Medinan", StringComparison.OrdinalIgnoreCase) || s.RevelationType.Contains("Madani", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(s =>
                s.SurahNum.ToString().Contains(query) ||
                s.NameTransliteration.ToLowerInvariant().Contains(query) ||
                s.NameEnglish.ToLowerInvariant().Contains(query) ||
                s.NameArabic.Contains(query));
        }

        foreach (var item in items)
        {
            FilteredSurahs.Add(item);
        }
    }

    public void NotifyUiStateChanged()
    {
        OnPropertyChanged(nameof(FabIconText));
        OnPropertyChanged(nameof(MicButtonColor));
        OnPropertyChanged(nameof(MicBorderColor));
        OnPropertyChanged(nameof(BlindModeButtonColor));
        OnPropertyChanged(nameof(TajweedButtonColor));
        OnPropertyChanged(nameof(AutoDetectButtonColor));
        OnPropertyChanged(nameof(StatusBannerBackgroundColor));
        OnPropertyChanged(nameof(StatusBannerTextColor));
        OnPropertyChanged(nameof(StatusBannerIcon));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsTranscribing));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(ProcessingSummary));
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

    private async Task SaveGuidedAttemptAsync(MatchResult result)
    {
        if (!IsGuidedAssignment || _sessionId is null || _attemptSaved)
        {
            return;
        }

        var isPerfect = result.Mismatches.Count == 0 && result.Confidence >= PerfectConfidenceThreshold;
        var isGoodEnough = result.Confidence >= MatchConfidenceThreshold;

        if (isPerfect)
        {
            _attemptSaved = true;
            try
            {
                await _verseRepository.SaveVerseAttemptAsync(
                    _session.CurrentUserEmail,
                    new CoreModels.VerseAttemptInput(
                        _sessionId.Value,
                        _assignmentId,
                        result.SurahNum,
                        result.AyahNum,
                        result.Confidence,
                        result.Confidence,
                        result.TranscriptionText,
                        result.Mismatches.Select(m => new CoreModels.RecitationWordMismatch(m.Position, m.Spoken, m.Expected)).ToArray(),
                        result.TajweedViolations.Select(v => new CoreModels.TajweedViolation(v.WordPosition, v.Rule, v.ExpectedWord, v.SpokenWord, v.Hint)).ToArray(),
                        MarkAssignmentComplete: true));
                await _verseRepository.CloseRecitationSessionAsync(_sessionId.Value, CoreModels.RecitationSessionStatus.Completed);
                _recitation.MarkCurrentAyahComplete();
                StatusMessage = "Assignment complete. Choose retry or continue to the next ayah.";
            }
            catch (Exception ex)
            {
                _attemptSaved = false;
                _diagnostics.Error("Could not persist guided recitation attempt.", ex);
                StatusMessage = "The result was matched, but could not be saved. Retry the assignment.";
            }
        }
        else if (isGoodEnough && !_attemptSaved)
        {
            _attemptSaved = true;
            try
            {
                await _verseRepository.SaveVerseAttemptAsync(
                    _session.CurrentUserEmail,
                    new CoreModels.VerseAttemptInput(
                        _sessionId.Value,
                        _assignmentId,
                        result.SurahNum,
                        result.AyahNum,
                        result.Confidence,
                        result.Confidence,
                        result.TranscriptionText,
                        result.Mismatches.Select(m => new CoreModels.RecitationWordMismatch(m.Position, m.Spoken, m.Expected)).ToArray(),
                        result.TajweedViolations.Select(v => new CoreModels.TajweedViolation(v.WordPosition, v.Rule, v.ExpectedWord, v.SpokenWord, v.Hint)).ToArray(),
                        MarkAssignmentComplete: false));
                StatusMessage = "Attempt saved. Keep reciting to improve, or stop to review corrections.";
            }
            catch (Exception ex)
            {
                _attemptSaved = false;
                _diagnostics.Error("Could not persist partial recitation attempt.", ex);
                StatusMessage = "Could not save the attempt. Keep trying or reset.";
            }
        }
    }

    private async Task SaveBestPartialAttemptAsync(CoreModels.RecitationSessionStatus sessionStatus)
    {
        if (!IsGuidedAssignment || _sessionId is null || _attemptSaved)
        {
            return;
        }

        var best = _bestSessionResult;
        if (best is null || best.Confidence < PartialAttemptSaveThreshold)
        {
            return;
        }

        _attemptSaved = true;
        try
        {
            await _verseRepository.SaveVerseAttemptAsync(
                _session.CurrentUserEmail,
                new CoreModels.VerseAttemptInput(
                    _sessionId.Value,
                    _assignmentId,
                    best.SurahNum,
                    best.AyahNum,
                    best.Confidence,
                    best.Confidence,
                    best.TranscriptionText,
                    best.Mismatches.Select(m => new CoreModels.RecitationWordMismatch(m.Position, m.Spoken, m.Expected)).ToArray(),
                    best.TajweedViolations.Select(v => new CoreModels.TajweedViolation(v.WordPosition, v.Rule, v.ExpectedWord, v.SpokenWord, v.Hint)).ToArray(),
                    MarkAssignmentComplete: false));
        }
        catch (Exception ex)
        {
            _attemptSaved = false;
            _diagnostics.Error("Could not save best partial attempt on session close.", ex);
        }
    }

    private void ResetCurrentResult()
    {
        Confidence = 0;
        ArabicText = string.Empty;
        TranscriptionText = string.Empty;
        IsTranscribing = false;
        Mismatches = [];
        TajweedViolations = [];
        HasTajweedViolations = false;
        HighlightedArabicText = new FormattedString();
        IsVerseCorrect = false;
        _attemptSaved = false;
        _bestSessionResult = null;
        _bestPrefixLength = 0;
        _bestMatchedWordCount = 0;
        _bestProcessedWordCount = 0;
        _bestConfidence = 0;
        _recitation.ClearLastMatchedPosition();
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

        if (message.Contains("transcript: \"", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(message, "transcript:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                ProcessSpokenText(match.Groups[1].Value);
            }
        }
    }

    [RelayCommand]
    private async Task StopRecitationAsync()
    {
        try
        {
            await _audio.StopRecordingAsync();
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Audio stop in StopRecitation: {ex.Message}");
        }

        try
        {
            await _recitation.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Disconnect in StopRecitation: {ex.Message}");
        }

        _stopwatch.Reset();
        _timer?.Stop();
        IsRecording = false;
        IsTranscribing = false;
        HasLiveToken = false;
        LiveTokenText = string.Empty;
        ElapsedTimeString = "00:00";
        MistakesCount = 0;
        Accuracy = 100.0;
        CurrentWordCursor = 0;

        foreach (var w in PageWords)
        {
            w.Status = WordRecitationStatus.Hidden;
        }

        StatusMessage = $"Reset: Surah {HeaderTitle} • Tap mic to recite";
        ProcessingSummary = "Awaiting recitation…";
        NotifyUiStateChanged();
        RequestCanvasRedraw?.Invoke();
    }

    [RelayCommand]
    private void ToggleBlindMode()
    {
        IsBlindRecitationMode = !IsBlindRecitationMode;
        OnPropertyChanged(nameof(BlindModeButtonColor));
    }

    [RelayCommand]
    private void ToggleTajweed()
    {
        IsTajweedMode = !IsTajweedMode;
        OnPropertyChanged(nameof(TajweedButtonColor));
    }

    [RelayCommand]
    private void ToggleAutoDetectMode()
    {
        IsFreeReciteMode = !IsFreeReciteMode;
        StatusMessage = IsFreeReciteMode
            ? "🌐 Auto-Detect Mode: Recite any verse from anywhere in the Quran"
            : $"📖 Surah Mode: Reciting {HeaderTitle}";
        NotifyUiStateChanged();
    }

    [RelayCommand]
    public async Task SelectSurahAsync(SurahInfo? surah)
    {
        if (surah is null) return;
        IsSurahPickerOpen = false;
        await LoadSurahAsync(surah.SurahNum, 1);
    }

    [RelayCommand]
    public async Task NextSurahAsync()
    {
        var nextSurah = Math.Min(114, SelectedSurah + 1);
        if (nextSurah != SelectedSurah)
        {
            await LoadSurahAsync(nextSurah, 1);
        }
    }

    [RelayCommand]
    public async Task PreviousSurahAsync()
    {
        var prevSurah = Math.Max(1, SelectedSurah - 1);
        if (prevSurah != SelectedSurah)
        {
            await LoadSurahAsync(prevSurah, 1);
        }
    }

    [RelayCommand]
    public void OpenSurahPicker()
    {
        SurahSearchText = string.Empty;
        SurahCategoryFilter = "All";
        FilterSurahs();
        IsSurahPickerOpen = true;
    }

    [RelayCommand]
    public void CloseSurahPicker()
    {
        IsSurahPickerOpen = false;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SurahSearchText = string.Empty;
    }

    [RelayCommand]
    public void SetCategoryFilter(string category)
    {
        SurahCategoryFilter = category;
    }

    public async Task LoadSurahAsync(int surahNum, int startAyah = 1)
    {
        if (IsRecording)
        {
            await ToggleRecordingAsync();
        }

        SelectedSurah = surahNum;
        SelectedAyah = startAyah;

        SelectedSurahInfo = Surahs.FirstOrDefault(s => s.SurahNum == surahNum);
        HeaderTitle = SelectedSurahInfo?.NameTransliteration ?? $"Surah {surahNum}";
        SurahBannerArabic = SelectedSurahInfo is not null
            ? $"سُورَةُ {SelectedSurahInfo.NameArabic}"
            : $"سُورَة {surahNum}";
        BismillahText = surahNum == 9 ? string.Empty : "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ";

        RebuildAyahNumbersForSurah(surahNum);

        int pageNum = await _verseRepository.GetPageForVerseAsync(surahNum, startAyah);
        if (pageNum <= 0) pageNum = (surahNum == 67 ? 562 : 1);

        var juzInfo = await _verseRepository.GetJuzForVerseAsync(surahNum, startAyah);
        var juzNum = juzInfo?.JuzNum ?? 1;

        HeaderSubtitle = $"Page {pageNum} | Juz {juzNum} | {surahNum}:{startAyah}";

        if (surahNum == 67 && pageNum == 562)
        {
            InitializePageLayout(_lastCanvasWidth, _lastCanvasHeight);
        }
        else
        {
            var verses = new List<Verse>();
            var maxAyah = SelectedSurahInfo?.AyahCount ?? 30;
            var endAyah = Math.Min(maxAyah, startAyah + 25);
            for (int a = startAyah; a <= endAyah; a++)
            {
                var v = await _verseRepository.GetVerseAsync(surahNum, a);
                if (v is not null) verses.Add(v);
            }

            var (words, markers) = MushafLayoutService.BuildLayoutFromVerses(verses, surahNum, pageNum, _lastCanvasWidth, _lastCanvasHeight);
            PageWords = words;
            AyahMarkers = markers;
            CanvasDrawable.Words = PageWords;
            CanvasDrawable.Markers = AyahMarkers;
            CanvasDrawable.IsBlindRecitationMode = IsBlindRecitationMode;
            CanvasDrawable.IsTajweedColored = IsTajweedMode;
            RequestCanvasRedraw?.Invoke();
        }

        CurrentWordCursor = 0;
        MistakesCount = 0;
        Accuracy = 100.0;
        ElapsedTimeString = "00:00";
        _stopwatch.Reset();
        HasLiveToken = false;
        LiveTokenText = string.Empty;

        _recitation.ClearSurahContext();
        _recitation.ClearLastMatchedPosition();
        _recitation.SetSurahContext(surahNum, SelectedSurahInfo?.NameArabic ?? string.Empty);

        StatusMessage = $"Ready: Surah {HeaderTitle} ({surahNum}:{startAyah}) • Tap mic to recite";
        ProcessingSummary = "Ready to recite.";
        NotifyUiStateChanged();
    }

    [RelayCommand]
    private void PreviousAyah()
    {
        if (PageWords.Count == 0) return;

        int currentAyah = PageWords[Math.Min(CurrentWordCursor, PageWords.Count - 1)].AyahNumber;
        int targetAyah = Math.Max(1, currentAyah - 1);

        for (int i = 0; i < PageWords.Count; i++)
        {
            if (PageWords[i].AyahNumber == targetAyah)
            {
                CurrentWordCursor = i;
                break;
            }
        }

        for (int i = CurrentWordCursor; i < PageWords.Count; i++)
        {
            PageWords[i].Status = WordRecitationStatus.Hidden;
        }

        HeaderSubtitle = $"Page 562 | Juz 29 | Hizb 57 | 67:{targetAyah}";
        RequestCanvasRedraw?.Invoke();
    }

    [RelayCommand]
    private void RewindPage()
    {
        CurrentWordCursor = 0;
        MistakesCount = 0;
        Accuracy = 100.0;
        HasLiveToken = false;
        LiveTokenText = string.Empty;

        foreach (var w in PageWords)
        {
            w.Status = WordRecitationStatus.Hidden;
        }

        HeaderSubtitle = "Page 562 | Juz 29 | Hizb 57 | 67:1";
        RequestCanvasRedraw?.Invoke();
    }

    private void OnAsrDownloadProgress(object? sender, AsrDownloadProgress progress)
    {
        // Surface Whisper model download/extraction progress in the UI.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsModelDownloading = true;
            ModelDownloadProgress = progress.Fraction is > 0 and <= 1
                ? progress.Fraction
                : ModelDownloadProgress;
            if (!string.IsNullOrWhiteSpace(progress.StatusMessage))
            {
                ModelDownloadStatus = progress.StatusMessage;
            }
        });
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

    private async Task AdvanceToNextAyahAsync(int surah, int ayah)
    {
        var next = RecitationPracticeSettings.GetNextAyah(surah, ayah);
        if (next is null)
        {
            StatusMessage = "You have completed the Quran. Continue in free recitation or choose a passage.";
            return;
        }

        SelectedSurah = next.Value.Surah;
        SelectedSurahInfo = Surahs.FirstOrDefault(item => item.SurahNum == next.Value.Surah);
        RebuildAyahNumbersForSurah(next.Value.Surah);
        SelectedAyah = next.Value.Ayah;
        OnPropertyChanged(nameof(CurrentAyahLabel));
        _recitation.SetSurahContext(next.Value.Surah, SelectedSurahInfo?.NameArabic ?? string.Empty);
        _recitation.ClearLastMatchedPosition();
        _recitation.ClearTranscriptContext();
        ResetCurrentResult();
        await LoadVerseAsync(next.Value.Surah, next.Value.Ayah);
        StatusMessage = $"Next: Surah {next.Value.Surah}:{next.Value.Ayah}. Continue reciting.";
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

    // ── Madrasa Quran Teacher (Al-Ustadh AI) Core State & Commands ──────────

    private MadrasaRecitationMode _currentMadrasaMode = MadrasaRecitationMode.Sabaq;
    public MadrasaRecitationMode CurrentMadrasaMode
    {
        get => _currentMadrasaMode;
        set
        {
            if (SetProperty(ref _currentMadrasaMode, value))
            {
                NotifyMadrasaModeChanged();
            }
        }
    }

    public Color SabaqTabBgColor => CurrentMadrasaMode == MadrasaRecitationMode.Sabaq ? Color.FromArgb("#065F46") : Color.FromArgb("#1E232B");
    public Color SabaqTabTextColor => CurrentMadrasaMode == MadrasaRecitationMode.Sabaq ? Color.FromArgb("#34D399") : Color.FromArgb("#94A3B8");
    public Color SabqiTabBgColor => CurrentMadrasaMode == MadrasaRecitationMode.Sabqi ? Color.FromArgb("#78350F") : Color.FromArgb("#1E232B");
    public Color SabqiTabTextColor => CurrentMadrasaMode == MadrasaRecitationMode.Sabqi ? Color.FromArgb("#FBBF24") : Color.FromArgb("#94A3B8");
    public Color ManzilTabBgColor => CurrentMadrasaMode == MadrasaRecitationMode.Manzil ? Color.FromArgb("#0C4A6E") : Color.FromArgb("#1E232B");
    public Color ManzilTabTextColor => CurrentMadrasaMode == MadrasaRecitationMode.Manzil ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8");
    public Color ExamTabBgColor => CurrentMadrasaMode == MadrasaRecitationMode.UstadhExam ? Color.FromArgb("#581C87") : Color.FromArgb("#1E232B");
    public Color ExamTabTextColor => CurrentMadrasaMode == MadrasaRecitationMode.UstadhExam ? Color.FromArgb("#C084FC") : Color.FromArgb("#94A3B8");

    public bool IsExamMode => CurrentMadrasaMode == MadrasaRecitationMode.UstadhExam;

    private MutashabihaChallenge? _currentExamChallenge;
    public MutashabihaChallenge? CurrentExamChallenge
    {
        get => _currentExamChallenge;
        set => SetProperty(ref _currentExamChallenge, value);
    }

    private string _examQuestionPrompt = string.Empty;
    public string ExamQuestionPrompt
    {
        get => _examQuestionPrompt;
        set => SetProperty(ref _examQuestionPrompt, value);
    }

    private int _luqmahCount;
    public int LuqmahCount { get => _luqmahCount; set => SetProperty(ref _luqmahCount, value); }

    private int _hesitationCount;
    public int HesitationCount { get => _hesitationCount; set => SetProperty(ref _hesitationCount, value); }

    private string _teacherPromptText = string.Empty;
    public string TeacherPromptText { get => _teacherPromptText; set => SetProperty(ref _teacherPromptText, value); }

    private bool _hasTeacherPrompt;
    public bool HasTeacherPrompt { get => _hasTeacherPrompt; set => SetProperty(ref _hasTeacherPrompt, value); }

    private bool _isEvaluationModalOpen;
    public bool IsEvaluationModalOpen { get => _isEvaluationModalOpen; set => SetProperty(ref _isEvaluationModalOpen, value); }

    private MadrasaEvaluation? _latestEvaluation;
    public MadrasaEvaluation? LatestEvaluation
    {
        get => _latestEvaluation;
        set
        {
            if (SetProperty(ref _latestEvaluation, value))
            {
                NotifyEvaluationPropertiesChanged();
            }
        }
    }

    public string EvaluationGradeDisplay => LatestEvaluation?.Grade switch
    {
        MadrasaGrade.Mumtaz => "🌟 ممتاز (Mumtaz - 100%)",
        MadrasaGrade.JayyidJiddan => "🟢 جيد جداً (Jayyid Jiddan)",
        MadrasaGrade.Jayyid => "🟡 جيد (Jayyid)",
        MadrasaGrade.Maqbool => "🟠 مقبول (Maqbool)",
        MadrasaGrade.Iadah or _ => "🔴 إعادة السبق غداً (Repeat)",
    } ?? string.Empty;

    public Color EvaluationGradeColor => LatestEvaluation?.Grade switch
    {
        MadrasaGrade.Mumtaz => Color.FromArgb("#10B981"),
        MadrasaGrade.JayyidJiddan => Color.FromArgb("#34D399"),
        MadrasaGrade.Jayyid => Color.FromArgb("#FBBF24"),
        MadrasaGrade.Maqbool => Color.FromArgb("#FB923C"),
        MadrasaGrade.Iadah or _ => Color.FromArgb("#EF4444"),
    } ?? Color.FromArgb("#10B981");

    public string EvaluationDecisionDisplay => LatestEvaluation?.Decision switch
    {
        MadrasaTeacherDecision.PassedToNext => "✅ مجاز - يُسمح بحفظ الدرس التالي بفضل الله",
        MadrasaTeacherDecision.ReviewWeakSpots => "⚠️ مراجعة - تثبيت الأخطاء والوقفات اليوم",
        MadrasaTeacherDecision.RepeatLesson or _ => "🔄 إعادة - يُعاد هذا السبق غداً للإتقان",
    } ?? string.Empty;

    public Color EvaluationDecisionColor => LatestEvaluation?.Decision switch
    {
        MadrasaTeacherDecision.PassedToNext => Color.FromArgb("#10B981"),
        MadrasaTeacherDecision.ReviewWeakSpots => Color.FromArgb("#F59E0B"),
        MadrasaTeacherDecision.RepeatLesson or _ => Color.FromArgb("#EF4444"),
    } ?? Color.FromArgb("#10B981");

    public string EvaluationRemarksArabic => LatestEvaluation?.RemarksArabic ?? string.Empty;
    public string EvaluationRemarksEnglish => LatestEvaluation?.RemarksEnglish ?? string.Empty;

    public string EvaluationModeDisplay => LatestEvaluation?.Mode switch
    {
        MadrasaRecitationMode.Sabaq => "السبق (Sabaq - New Lesson)",
        MadrasaRecitationMode.Sabqi => "السبقي (Sabqi - Recent Revision)",
        MadrasaRecitationMode.Manzil => "المنزل (Manzil - Cumulative Dhor)",
        MadrasaRecitationMode.UstadhExam => "اختبار الشيخ والمتشابهات",
        _ => "تسميع"
    } ?? string.Empty;

    [RelayCommand]
    public void SetMadrasaMode(string modeName)
    {
        if (Enum.TryParse<MadrasaRecitationMode>(modeName, true, out var mode))
        {
            CurrentMadrasaMode = mode;
            if (mode == MadrasaRecitationMode.UstadhExam)
            {
                LoadExamQuestion();
            }
            else
            {
                var nameAr = mode switch
                {
                    MadrasaRecitationMode.Sabaq => "السبق (الدرس الجديد)",
                    MadrasaRecitationMode.Sabqi => "السبقي (مراجعة القريب)",
                    MadrasaRecitationMode.Manzil => "المنزل (المراجعة الكبرى)",
                    _ => "التسميع"
                };
                StatusMessage = $"وضع الشيخ: {nameAr} • اضغط المايك للتسميع";
            }
        }
    }

    [RelayCommand]
    public void NextExamQuestion()
    {
        LoadExamQuestion();
    }

    private void LoadExamQuestion()
    {
        CurrentExamChallenge = MutashabihatDatabase.GetRandomChallenge();
        ExamQuestionPrompt = $"﴿ {CurrentExamChallenge.PromptVerseArabic} ﴾";
        StatusMessage = $"اختبار الشيخ: أكمل من {CurrentExamChallenge.SurahNameArabic}";
        _ = LoadSurahAsync(CurrentExamChallenge.SurahNum, CurrentExamChallenge.AyahNum);
    }

    private void NotifyMadrasaModeChanged()
    {
        OnPropertyChanged(nameof(SabaqTabBgColor));
        OnPropertyChanged(nameof(SabaqTabTextColor));
        OnPropertyChanged(nameof(SabqiTabBgColor));
        OnPropertyChanged(nameof(SabqiTabTextColor));
        OnPropertyChanged(nameof(ManzilTabBgColor));
        OnPropertyChanged(nameof(ManzilTabTextColor));
        OnPropertyChanged(nameof(ExamTabBgColor));
        OnPropertyChanged(nameof(ExamTabTextColor));
        OnPropertyChanged(nameof(IsExamMode));
    }

    private void NotifyEvaluationPropertiesChanged()
    {
        OnPropertyChanged(nameof(EvaluationGradeDisplay));
        OnPropertyChanged(nameof(EvaluationGradeColor));
        OnPropertyChanged(nameof(EvaluationDecisionDisplay));
        OnPropertyChanged(nameof(EvaluationDecisionColor));
        OnPropertyChanged(nameof(EvaluationRemarksArabic));
        OnPropertyChanged(nameof(EvaluationRemarksEnglish));
        OnPropertyChanged(nameof(EvaluationModeDisplay));
    }

    [RelayCommand]
    public async Task GiveLuqmahAsync()
    {
        if (CurrentWordCursor >= PageWords.Count) return;
        var targetWord = PageWords[CurrentWordCursor].UthmaniText;
        PageWords[CurrentWordCursor].Status = WordRecitationStatus.Revealed;
        CurrentWordCursor++;
        LuqmahCount++;
        RecalculateAccuracy();
        TeacherPromptText = targetWord;
        HasTeacherPrompt = true;
        StatusMessage = $"لقمة الشيخ: «{targetWord}»";
        RequestCanvasRedraw?.Invoke();
        await _madrasaAudio.SpeakLuqmahPromptAsync(targetWord);
    }

    [RelayCommand]
    public async Task CompleteRecitationSessionAsync()
    {
        if (CurrentWordCursor == 0 && MistakesCount == 0 && LuqmahCount == 0)
        {
            await StopRecitationAsync();
            return;
        }

        try
        {
            await _audio.StopRecordingAsync();
        }
        catch {}

        _stopwatch.Stop();
        _timer?.Stop();
        IsRecording = false;
        IsTranscribing = false;

        int endAyah = CurrentWordCursor > 0 && CurrentWordCursor <= PageWords.Count
            ? PageWords[Math.Min(CurrentWordCursor, PageWords.Count) - 1].AyahNumber
            : SelectedAyah;

        var input = new MadrasaEvaluationInput(
            UserKey: _userEmail,
            SessionId: _sessionId,
            SurahNum: SelectedSurah,
            AyahNum: SelectedAyah,
            EndAyahNum: endAyah,
            Mode: CurrentMadrasaMode,
            MistakesCount: MistakesCount,
            TajweedNotesCount: TajweedViolations.Count,
            LuqmahCount: LuqmahCount,
            HesitationCount: HesitationCount,
            AccuracyPercentage: Accuracy
        );

        LatestEvaluation = await _verseRepository.RecordMadrasaEvaluationAsync(input);
        IsEvaluationModalOpen = true;

        var spokenVerdict = MadrasaPedagogyEngine.GetSpokenVerdictArabic(LatestEvaluation.Grade, LatestEvaluation.Decision);
        _ = _madrasaAudio.SpeakTeacherVerdictAsync(spokenVerdict);
    }

    [RelayCommand]
    public void CloseEvaluationModal()
    {
        IsEvaluationModalOpen = false;
    }

    [RelayCommand]
    public async Task SaveEvaluationAndAdvanceAsync()
    {
        IsEvaluationModalOpen = false;
        await StopRecitationAsync();

        if (LatestEvaluation?.Decision == MadrasaTeacherDecision.PassedToNext)
        {
            await AdvanceToNextAyahAsync(SelectedSurah, SelectedAyah);
        }
    }

    [RelayCommand]
    public async Task RetryRecitationAsync()
    {
        IsEvaluationModalOpen = false;
        await StopRecitationAsync();
        await ToggleRecordingAsync();
    }
}
