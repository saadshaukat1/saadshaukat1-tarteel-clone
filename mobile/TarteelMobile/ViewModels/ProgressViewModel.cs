using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly IVerseRepository _verses;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;

    [ObservableProperty] private List<VerseProgress> _verseProgress = [];
    [ObservableProperty] private string _overallSummary = string.Empty;

    public ProgressViewModel(
        IVerseRepository verses,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _verses = verses;
        _session = session;
        _diagnostics = diagnostics;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var results = await _verses.GetProgressAsync(_session.CurrentUserEmail);
        _diagnostics.Info($"Loaded {results.Count} progress record(s) for progress view.");
        VerseProgress = [.. results];

        if (results.Count == 0)
        {
            OverallSummary = "No verses practiced yet. Start reciting!";
        }
        else
        {
            var avg = results.Average(r => r.MasteryScore);
            var mastered = results.Count(r => r.MasteryScore >= 0.9);
            OverallSummary = $"{results.Count} verse(s) practiced • {mastered} mastered • Avg accuracy {avg:P0}";
        }
    }
}

