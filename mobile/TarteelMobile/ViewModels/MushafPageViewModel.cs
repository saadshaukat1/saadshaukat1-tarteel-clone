using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

/// <summary>
/// Drives the 16-line Mushaf page view: loads the contiguous verse range for a
/// given Madani mushaf page, supports continuous navigation, and highlights the
/// verse currently being recited.
/// </summary>
public partial class MushafPageViewModel : ObservableObject
{
    private const int TotalPages = 604;

    private readonly IVerseRepository _verseRepository;
    private readonly IAppDiagnosticsService _diagnostics;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private IReadOnlyList<Verse> _pageVerses = [];

    [ObservableProperty]
    private string _pageLabel = "Page 1 / 604";

    [ObservableProperty]
    private string _verseRangeLabel = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPageData;

    // "surah:ayah" of the verse to highlight from recitation, or empty.
    [ObservableProperty]
    private string _highlightedVerseKey = string.Empty;

    public int PageCount => TotalPages;

    public MushafPageViewModel(IVerseRepository verseRepository, IAppDiagnosticsService diagnostics)
    {
        _verseRepository = verseRepository;
        _diagnostics = diagnostics;
    }

    [RelayCommand]
    public async Task LoadInitialAsync()
    {
        await LoadPageAsync(1);
    }

    [RelayCommand]
    public async Task LoadPageAsync(int page)
    {
        var clamped = Math.Clamp(page, 1, TotalPages);
        if (clamped == CurrentPage && PageVerses.Count > 0)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var mushafPage = await _verseRepository.GetPageAsync(clamped);
            if (mushafPage is null)
            {
                PageVerses = [];
                HasPageData = false;
                PageLabel = $"Page {clamped} / {TotalPages}";
                VerseRangeLabel = "No page data";
                return;
            }

            var verses = await LoadVerseRangeAsync(mushafPage);
            PageVerses = verses;
            HasPageData = verses.Count > 0;
            CurrentPage = clamped;
            PageLabel = $"Page {clamped} / {TotalPages}";
            VerseRangeLabel = mushafPage.VerseRangeLabel;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Failed to load mushaf page {clamped}: {ex.Message}");
            PageVerses = [];
            HasPageData = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task GoToNextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            await LoadPageAsync(CurrentPage + 1);
        }
    }

    [RelayCommand]
    public async Task GoToPreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            await LoadPageAsync(CurrentPage - 1);
        }
    }

    /// <summary>Navigates to the page containing the given verse and highlights it.</summary>
    public async Task ShowVerseAsync(int surahNum, int ayahNum)
    {
        try
        {
            await _verseRepository.EnsureInitializedAsync();
            var page = await _verseRepository.GetPageForVerseAsync(surahNum, ayahNum);
            HighlightedVerseKey = $"{surahNum}:{ayahNum}";
            await LoadPageAsync(page);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Failed to show verse {surahNum}:{ayahNum} in mushaf: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<Verse>> LoadVerseRangeAsync(MushafPage page)
    {
        var verses = new List<Verse>();
        if (page.StartSurah == page.EndSurah)
        {
            for (var ayah = page.StartAyah; ayah <= page.EndAyah; ayah++)
            {
                var verse = await _verseRepository.GetVerseAsync(page.StartSurah, ayah);
                if (verse is not null)
                {
                    verses.Add(verse);
                }
            }
        }
        else
        {
            // Spanning surah boundary: load tail of start surah, then head of end surah.
            for (var ayah = page.StartAyah; ; ayah++)
            {
                var verse = await _verseRepository.GetVerseAsync(page.StartSurah, ayah);
                if (verse is null)
                {
                    break;
                }

                verses.Add(verse);
            }

            for (var ayah = 1; ayah <= page.EndAyah; ayah++)
            {
                var verse = await _verseRepository.GetVerseAsync(page.EndSurah, ayah);
                if (verse is not null)
                {
                    verses.Add(verse);
                }
            }
        }

        return verses;
    }
}

