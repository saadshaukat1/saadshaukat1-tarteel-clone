using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using TarteelClone.LocalRecitationCore.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly IVerseRepository _verses;
    private readonly ITodayWorkflowService _today;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;

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

    public ProgressViewModel(
        IVerseRepository verses,
        ITodayWorkflowService today,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _verses = verses;
        _today = today;
        _session = session;
        _diagnostics = diagnostics;
        _ = LoadAsync();
    }

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
            var results = await _verses.GetProgressAsync(_session.CurrentUserEmail);
            TodayAssignments = await _today.GetTodayAssignmentsAsync(_session.CurrentUserEmail);
            TajweedSummaries = await _verses.GetTajweedRuleSummariesAsync(_session.CurrentUserEmail);
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
}
