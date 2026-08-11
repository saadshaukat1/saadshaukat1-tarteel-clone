using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelMobile.Models;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class MushafPageView : ContentPage
{
    private const int RowsPerPage = 16;
    private const double RowHeight = 54;

    private readonly MushafPageViewModel _viewModel;
    private bool _autoAdvanceArmed = true;
    private bool _isRendering;

    public MushafPageView(MushafPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.PageVerses.Count == 0)
        {
            await _viewModel.LoadInitialAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MushafPageViewModel.PageVerses)
            or nameof(MushafPageViewModel.HighlightedVerseKey))
        {
            RenderLines();
        }
    }

    private void RenderLines()
    {
        if (_isRendering)
            return;

        _isRendering = true;
        try
        {
            LinesGrid.Children.Clear();

            var verses = _viewModel.PageVerses;
            if (verses.Count == 0)
                return;

            var lines = Build16Lines(verses);

            for (var lineIdx = 0; lineIdx < RowsPerPage; lineIdx++)
            {
                var lineText = lineIdx < lines.Count ? lines[lineIdx].Text : string.Empty;
                var lineKey = lineIdx < lines.Count ? lines[lineIdx].VerseKey : string.Empty;
                var isHighlighted = !string.IsNullOrEmpty(lineKey)
                    && string.Equals(lineKey, _viewModel.HighlightedVerseKey, StringComparison.Ordinal);

                var label = new Label
                {
                    Text = lineText,
                    FontFamily = (string)Application.Current!.Resources["ArabicFontFamily"],
                    FontSize = (double)Application.Current.Resources["FontSizeVerse"],
                    LineHeight = (double)Application.Current.Resources["LineHeightArabic"],
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    LineBreakMode = LineBreakMode.NoWrap,
                    MaximumWidthRequest = 680,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Thickness(6, 0),
                    HeightRequest = RowHeight,
                    TextColor = isHighlighted
                        ? (Color)Application.Current.Resources["PrimaryDark"]
                        : (Color)Application.Current.Resources["OnSurface"],
                    FontAttributes = isHighlighted ? FontAttributes.Bold : FontAttributes.None,
                    AutomationId = string.IsNullOrEmpty(lineKey)
                        ? $"mushaf-line-{lineIdx}"
                        : $"mushaf-verse-{lineKey}-line-{lineIdx}"
                };

                var cell = new Border
                {
                    StrokeThickness = 0,
                    BackgroundColor = isHighlighted
                        ? (Color)Application.Current.Resources["SurfaceHover"]
                        : Colors.Transparent,
                    Content = label,
                    HeightRequest = RowHeight
                };

                Grid.SetRow(cell, lineIdx);
                LinesGrid.Children.Add(cell);
            }
        }
        finally
        {
            _isRendering = false;
        }
    }

    /// <summary>
    /// Builds exactly 16 lines from the page's verses, producing a layout
    /// that reads like a printed Madani Mushaf. Handles Bismillah placement at
    /// surah boundaries, proportional word distribution across lines, and
    /// verse-end markers at line boundaries.
    /// </summary>
    private List<(string Text, string VerseKey)> Build16Lines(IReadOnlyList<Verse> verses)
    {
        if (verses.Count == 0)
            return [];

        var isSurahStart = verses[0].AyahNum == 1;
        var bismillahLines = isSurahStart ? 1 : 0;
        var availableLines = RowsPerPage - bismillahLines;

        // Collect all (word, verseKey, isVerseEndMarker) tuples.
        // Verse-end marker (۩) is appended to the last word of each verse that
        // is NOT the final verse on the page (final verse continues onto next page).
        var allWords = new List<(string Word, string VerseKey, bool IsEndMarker)>();

        for (var vi = 0; vi < verses.Count; vi++)
        {
            var verse = verses[vi];
            var key = $"{verse.SurahNum}:{verse.AyahNum}";
            var words = verse.ArabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var wi = 0; wi < words.Length; wi++)
            {
                var isEnd = wi == words.Length - 1 && vi < verses.Count - 1;
                allWords.Add((words[wi], key, isEnd));
            }

            // Insert verse separator (۞) after each verse except the last on the page
            if (vi < verses.Count - 1)
            {
                allWords.Add(("۞", key, false));
            }
        }

        if (allWords.Count == 0)
            return [];

        // For pages with very few words (like Fatiha), spread one word per line
        // to fill the page naturally rather than cramming everything onto one line.
        if (allWords.Count <= availableLines)
        {
            var result = new List<(string Text, string VerseKey)>();

            // Bismillah on surah-start pages
            if (isSurahStart)
            {
                result.Add(("ۢبِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ", $"{verses[0].SurahNum}:0"));
            }

            for (var i = 0; i < availableLines; i++)
            {
                if (i < allWords.Count)
                {
                    var (word, key, isEnd) = allWords[i];
                    var text = isEnd ? $"{word} ۩" : word;
                    result.Add((text, key));
                }
                else
                {
                    result.Add((" ", string.Empty));
                }
            }

            return result;
        }

        // Even distribution: each line gets totalWords / availableLines words, with the
        // remainder spread evenly across lines (Bresenham-style) instead of stacked at the
        // top. This yields a gentler line-length cadence closer to a printed Mushaf than a
        // front-loaded word dump.
        var wordsPerLine = allWords.Count / availableLines;
        var remainder = allWords.Count % availableLines;

        var lines = new List<(string Text, string VerseKey)>();

        // Bismillah on surah-start pages
        if (isSurahStart)
        {
            lines.Add(("ۢبِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ", $"{verses[0].SurahNum}:0"));
        }

        var wordIndex = 0;
        var remainderAccum = 0;
        for (var lineIdx = 0; lineIdx < availableLines; lineIdx++)
        {
            var wordsOnThisLine = wordsPerLine;
            remainderAccum += remainder;
            if (remainderAccum >= availableLines)
            {
                wordsOnThisLine++;
                remainderAccum -= availableLines;
            }
            var lineBuilder = new System.Text.StringBuilder();
            var lineKey = string.Empty;

            for (var w = 0; w < wordsOnThisLine && wordIndex < allWords.Count; w++)
            {
                var (word, key, isEnd) = allWords[wordIndex];
                if (lineBuilder.Length > 0)
                    lineBuilder.Append(' ');
                lineBuilder.Append(word);
                lineKey = key;

                // Append verse-end marker at the last word if it marks a verse boundary
                if (isEnd)
                    lineBuilder.Append(" ۩");

                wordIndex++;
            }

            lines.Add((lineBuilder.ToString(), lineKey));
        }

        // Pad to exactly 16 lines
        var result2 = new List<(string Text, string VerseKey)>();
        for (var i = 0; i < RowsPerPage; i++)
        {
            result2.Add(i < lines.Count ? lines[i] : (" ", string.Empty));
        }

        return result2;
    }

    private void OnPageScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scroll)
            return;

        var contentHeight = scroll.ContentSize.Height;
        var viewportHeight = scroll.Height;
        if (viewportHeight <= 0 || contentHeight <= viewportHeight)
            return;

        var threshold = contentHeight - viewportHeight - 40;
        if (e.ScrollY >= threshold && _autoAdvanceArmed && _viewModel.CurrentPage < _viewModel.PageCount)
        {
            _autoAdvanceArmed = false;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _viewModel.GoToNextPageAsync();
                if (scroll.ContentSize.Height > 0)
                {
                    await scroll.ScrollToAsync(0, 0, false);
                }
                _autoAdvanceArmed = true;
            });
        }
    }
}
