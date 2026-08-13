using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using TarteelClone.LocalRecitationCore.Models;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly IVerseRepository _verses;
    private readonly ITodayWorkflowService _today;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly ICurriculumService _curriculum;

    private List<Models.VerseProgress> _verseProgress = [];
    public List<Models.VerseProgress> VerseProgress { get => _verseProgress; set => SetProperty(ref _verseProgress, value); }

    private string _overallSummary = string.Empty;
    public string OverallSummary { get => _overallSummary; set => SetProperty(ref _overallSummary, value); }

    private IReadOnlyList<TodayAssignment> _todayAssignments = [];
    public IReadOnlyList<TodayAssignment> TodayAssignments { get => _todayAssignments; private set => SetProperty(ref _todayAssignments, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    private string _todayMessage = "Preparing today’s practice…";
    public string TodayMessage { get => _todayMessage; private set => SetProperty(ref _todayMessage, value); }

    private IReadOnlyList<TajweedRuleSummary> _tajweedSummaries = [];
    public IReadOnlyList<TajweedRuleSummary> TajweedSummaries { get => _tajweedSummaries; private set => SetProperty(ref _tajweedSummaries, value); }

    public bool HasTajweedSummary => TajweedSummaries.Count > 0;

    public TodayAssignment? CurrentTodayAssignment => TodayAssignments.FirstOrDefault();
    public bool HasTodayAssignments => TodayAssignments.Count > 0;
    public string DueSummary => TodayAssignments.Count.ToString();

    // ── Student dashboard surface ────────────────────────────────────────────

    private string _streakDisplay = "No streak yet";
    public string StreakDisplay { get => _streakDisplay; private set => SetProperty(ref _streakDisplay, value); }

    private string _pathName = "Juz 30";
    public string PathName { get => _pathName; private set => SetProperty(ref _pathName, value); }

    private string _pathProgress = string.Empty;
    public string PathProgress { get => _pathProgress; private set => SetProperty(ref _pathProgress, value); }

    private string _goalLabel = "Daily goal: 1 new · 5 reviews";
    public string GoalLabel { get => _goalLabel; private set => SetProperty(ref _goalLabel, value); }

    private int _todayNewCount;
    public int TodayNewCount { get => _todayNewCount; private set => SetProperty(ref _todayNewCount, value); }

    private int _todayReviewCount;
    public int TodayReviewCount { get => _todayReviewCount; private set => SetProperty(ref _todayReviewCount, value); }

    private IReadOnlyList<WeakVerseRecommendation> _weakVerses = [];
    public IReadOnlyList<WeakVerseRecommendation> WeakVerses { get => _weakVerses; private set => SetProperty(ref _weakVerses, value); }

    public bool HasWeakVerses => WeakVerses.Count > 0;

    private string _recommendationText = string.Empty;
    public string RecommendationText { get => _recommendationText; private set => SetProperty(ref _recommendationText, value); }

    private string _apiStatus = string.Empty;
    public string ApiStatus { get => _apiStatus; private set => SetProperty(ref _apiStatus, value); }

    public ProgressViewModel(
        IVerseRepository verses,
        ITodayWorkflowService today,
        ISessionService session,
        IAppDiagnosticsService diagnostics,
        ICurriculumService curriculum,
        TarteelClone.Api.ILocalApi? localApi = null)
    {
        _verses = verses;
        _today = today;
        _session = session;
        _diagnostics = diagnostics;
        _curriculum = curriculum;
        _localApi = localApi;
        _ = LoadAsync();
    }

    private readonly TarteelClone.Api.ILocalApi? _localApi;

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task StartTodayAsync()
    {
        var assignment = CurrentTodayAssignment;
        if (assignment is not null)
        {
            await StartAssignmentAsync(assignment);
        }
    }

    [RelayCommand]
    private async Task StartAssignmentAsync(TodayAssignment? assignment)
    {
        if (assignment is null || IsLoading)
        {
            return;
        }

        var route = $"//RecitationPage?assignmentId={assignment.Assignment.Id}&surah={assignment.Assignment.SurahNum}&ayah={assignment.Assignment.AyahNum}&reason={Uri.EscapeDataString(assignment.ReasonDisplay)}";
        await Shell.Current.GoToAsync(route);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var userKey = _session.CurrentUserEmail;
            var results = await _verses.GetProgressAsync(userKey);
            TodayAssignments = await _today.GetTodayAssignmentsAsync(userKey);
            TajweedSummaries = await _verses.GetTajweedRuleSummariesAsync(userKey);
            var streak = await _verses.GetStreakAsync(userKey);
            var (path, position) = await _verses.GetCurriculumPositionAsync(userKey);
            var pathVerses = _curriculum.GetLearningPath(path);
            var weak = await _verses.GetWeakVerseRecommendationsAsync(userKey);

            var newCount = TodayAssignments.Count(a => a.Assignment.Reason == LessonAssignmentReason.NewLesson);
            var reviewCount = TodayAssignments.Count(a => a.Assignment.Reason == LessonAssignmentReason.Review);
            TodayNewCount = newCount;
            TodayReviewCount = reviewCount;

            StreakDisplay = streak.Current > 0
                ? $"{streak.Current}-day streak · best {streak.Best}"
                : "No current streak";
            PathName = PathDisplayName(path);
            PathProgress = pathVerses.Count > 0
                ? $"{Math.Min(position, pathVerses.Count)} / {pathVerses.Count} verses"
                : "Path empty";
            GoalLabel = "Daily goal: {DailyNewLessonTarget} new · {DailyReviewTarget} reviews";

            WeakVerses = weak;
            OnPropertyChanged(nameof(HasWeakVerses));
            RecommendationText = weak.Count > 0
                ? $"Repeat {weak.Count} weak verse(s), then continue {PathName}."
                : $"Continue your {PathName} path at a steady pace.";

            if (_localApi is not null)
            {
                var status = _localApi.GetStatus();
                ApiStatus = status.IsReady ? $"Offline API {status.Version} ready" : $"Offline API unavailable: {status.Message}";
            }

            OnPropertyChanged(nameof(CurrentTodayAssignment));
            OnPropertyChanged(nameof(HasTodayAssignments));
            OnPropertyChanged(nameof(DueSummary));
            OnPropertyChanged(nameof(HasTajweedSummary));
            _diagnostics.Info($"Loaded {results.Count} progress record(s), {TodayAssignments.Count} Today assignment(s), and {TajweedSummaries.Count} tajweed rule group(s).");
            VerseProgress = [.. results];
            TodayMessage = TodayAssignments.Count == 0
                ? "Today is complete. Keep your current pace."
                : $"Start with {TodayAssignments[0].ReasonDisplay.ToLowerInvariant()} · {TodayAssignments.Count} item(s) ready.";

            if (results.Count == 0)
            {
                OverallSummary = "No verses practiced yet. Start reciting!";
            }
            else
            {
                var avg = results.Average(r => r.MasteryScore);
                var mastered = results.Count(r => r.MasteryScore >= 0.9);
                OverallSummary = $"{results.Count} verse(s) practiced | {mastered} mastered | Avg accuracy {avg:P0}";
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Could not load Today practice state.", ex);
            TodayAssignments = [];
            OnPropertyChanged(nameof(CurrentTodayAssignment));
            OnPropertyChanged(nameof(HasTodayAssignments));
            OnPropertyChanged(nameof(DueSummary));
            TodayMessage = "Today’s practice could not be loaded. Refresh to try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string PathDisplayName(CurriculumPath path) => path switch
    {
        CurriculumPath.Juz30 => "Juz 30 (Juz 'Amma)",
        CurriculumPath.ShortSurahs => "Short surahs first",
        CurriculumPath.Sequential => "Full Quran in order",
        _ => "Juz 30 (Juz 'Amma)"
    };
}
