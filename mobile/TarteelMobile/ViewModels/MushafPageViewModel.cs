using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelClone.LocalRecitationCore.Models;
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

    private int _currentPage = 1;
    public int CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }

    private IReadOnlyList<Verse> _pageVerses = [];
    public IReadOnlyList<Verse> PageVerses { get => _pageVerses; set => SetProperty(ref _pageVerses, value); }

    private string _pageLabel = "Page 1 / 604";
    public string PageLabel { get => _pageLabel; set => SetProperty(ref _pageLabel, value); }

    private string _verseRangeLabel = string.Empty;
    public string VerseRangeLabel { get => _verseRangeLabel; set => SetProperty(ref _verseRangeLabel, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

    private bool _hasPageData;
    public bool HasPageData { get => _hasPageData; set => SetProperty(ref _hasPageData, value); }

    private string _highlightedVerseKey = string.Empty;
    public string HighlightedVerseKey { get => _highlightedVerseKey; set => SetProperty(ref _highlightedVerseKey, value); }

    public int PageCount => TotalPages;
    public IReadOnlyList<JuzInfo> JuzList { get; private set; } = [];
    public IReadOnlyList<SurahInfo> SurahList { get; private set; } = [];
    public JuzInfo? SelectedJuz { get; set; }
    public SurahInfo? SelectedSurah { get; set; }
    public IReadOnlyList<int> AyahNumbers { get; private set; } = [];
    public int? SelectedAyah { get; set; }

    public MushafPageViewModel(IVerseRepository verseRepository, IAppDiagnosticsService diagnostics)
    {
        _verseRepository = verseRepository;
        _diagnostics = diagnostics;
    }

    [RelayCommand]
    public async Task LoadInitialAsync()
    {
        await _verseRepository.EnsureInitializedAsync();
        JuzList = await _verseRepository.GetAllJuzAsync();
        SurahList = await _verseRepository.GetAllSurahsAsync();
        OnPropertyChanged(nameof(JuzList));
        OnPropertyChanged(nameof(SurahList));
        await LoadPageAsync(1);
    }

    [RelayCommand]
    private async Task JumpToJuzAsync()
    {
        if (SelectedJuz is null)
        {
            return;
        }

        await LoadPageAsync(await _verseRepository.GetPageForVerseAsync(SelectedJuz.StartSurah, SelectedJuz.StartAyah));
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

