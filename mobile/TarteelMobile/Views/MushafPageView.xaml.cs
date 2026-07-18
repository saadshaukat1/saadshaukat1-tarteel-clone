using Microsoft.Maui.Controls;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class MushafPageView : ContentPage
{
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
        LinesGrid.RowDefinitions.Clear();

        var verses = _viewModel.PageVerses;
        for (var i = 0; i < verses.Count; i++)
        {
            LinesGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var index = 0; index < verses.Count; index++)
        {
            var verse = verses[index];
            var key = $"{verse.SurahNum}:{verse.AyahNum}";
            var isHighlighted = string.Equals(key, _viewModel.HighlightedVerseKey, StringComparison.Ordinal);

            var label = new Label
            {
                Text = verse.ArabicText,
                FontFamily = (string)Application.Current!.Resources["ArabicFontFamily"],
                FontSize = (double)Application.Current.Resources["FontSizeVerse"],
                LineHeight = (double)Application.Current.Resources["LineHeightArabic"],
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap,
                MaximumWidthRequest = 680,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Thickness(6, 2),
                AutomationId = $"mushaf-verse-{key}"
            };

            var cell = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = isHighlighted
                    ? Color.FromArgb("#EBF5EE")
                    : Colors.Transparent,
                Content = label
            };

            // Each verse occupies its own line, laid out sequentially top-to-bottom.
            // A standard Madani page fits ~15–16 lines; longer pages simply scroll.
            Grid.SetRow(cell, index);
            LinesGrid.Children.Add(cell);
        }
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

        // Auto-advance to the next page once the user scrolls near the bottom.
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
