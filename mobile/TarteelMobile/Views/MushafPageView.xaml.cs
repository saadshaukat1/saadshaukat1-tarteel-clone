using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelMobile.Models;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class MushafPageView : ContentPage
{
    private const int RowsPerPage = 16;
    private const int ApproxCharsPerLine = 34;
    private const double RowHeight = 48;

    private readonly MushafPageViewModel _viewModel;
    private bool _autoAdvanceArmed = true;

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
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
                LineBreakMode = LineBreakMode.NoWrap,
                MaximumWidthRequest = 680,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Thickness(6, 0),
                HeightRequest = RowHeight,
                AutomationId = string.IsNullOrEmpty(lineKey)
                    ? $"mushaf-line-{lineIdx}"
                    : $"mushaf-verse-{lineKey}-line-{lineIdx}"
            };

            var cell = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = isHighlighted
                    ? Color.FromArgb("#EBF5EE")
                    : Colors.Transparent,
                Content = label,
                HeightRequest = RowHeight
            };

            Grid.SetRow(cell, lineIdx);
            LinesGrid.Children.Add(cell);
        }
    }

    private static List<(string Text, string VerseKey)> Build16Lines(IReadOnlyList<Verse> verses)
    {
        var allWords = new List<(string Word, string Key)>();

        for (var vi = 0; vi < verses.Count; vi++)
        {
            var verse = verses[vi];
            var key = $"{verse.SurahNum}:{verse.AyahNum}";
            var words = verse.ArabicText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var wi = 0; wi < words.Length; wi++)
            {
                allWords.Add((words[wi], key));
            }

            if (vi < verses.Count - 1)
            {
                allWords.Add(("۞", key));
            }
        }

        if (allWords.Count == 0)
            return [];

        var lines = new List<(string Text, string VerseKey)>();
        var currentLine = new System.Text.StringBuilder();
        var currentKey = allWords[0].Key;

        for (var i = 0; i < allWords.Count; i++)
        {
            var (word, key) = allWords[i];
            var tentative = currentLine.Length == 0
                ? word
                : currentLine.ToString() + " " + word;

            if (tentative.Length <= ApproxCharsPerLine)
            {
                if (currentLine.Length > 0)
                    currentLine.Append(' ');
                currentLine.Append(word);
                if (key != currentKey)
                    currentKey = key;
                continue;
            }

            lines.Add((currentLine.ToString(), currentKey));

            currentLine.Clear();
            currentLine.Append(word);
            currentKey = key;
        }

        if (currentLine.Length > 0)
            lines.Add((currentLine.ToString(), currentKey));

        var result = new List<(string Text, string VerseKey)>();
        for (var i = 0; i < RowsPerPage; i++)
        {
            if (i < lines.Count)
            {
                result.Add(lines[i]);
            }
            else
            {
                result.Add((" ", string.Empty));
            }
        }

        return result;
    }

    private void OnPageScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scroll)
        {
            return;
        }

        var contentHeight = scroll.ContentSize.Height;
        var viewportHeight = scroll.Height;
        if (viewportHeight <= 0 || contentHeight <= viewportHeight)
        {
            return;
        }

        var threshold = contentHeight - viewportHeight - 40;
        if (e.ScrollY >= threshold && _autoAdvanceArmed && _viewModel.CurrentPage < _viewModel.PageCount)
        {
            _autoAdvanceArmed = false;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _viewModel.GoToNextPageAsync();
                await scroll.ScrollToAsync(0, 0, false);
                _autoAdvanceArmed = true;
            });
        }
    }
}
