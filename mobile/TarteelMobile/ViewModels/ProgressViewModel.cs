using CommunityToolkit.Mvvm.ComponentModel;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

public partial class ProgressViewModel : ObservableObject
{
    private readonly IVerseRepository _verses;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;

    [ObservableProperty] private List<Verse> _memorizedVerses = [];

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

    private async Task LoadAsync()
    {
        var results = await _verses.GetMemorizedVersesAsync(_session.CurrentUserEmail);
        _diagnostics.Info($"Loaded {results.Count} local memorized verse(s) for progress view.");
        MemorizedVerses = [.. results];
    }
}
