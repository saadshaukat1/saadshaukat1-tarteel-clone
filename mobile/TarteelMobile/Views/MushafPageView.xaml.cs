using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelClone.QuranEngine;
using TarteelMobile.Models;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class MushafPageView : ContentPage
{
    private const int RowsPerPage = 16;
    private const double RowHeight = 54;

    private readonly MushafPageViewModel _viewModel;
    private readonly IQuranPageLayout _layout;
    private bool _autoAdvanceArmed = true;
    private bool _isRendering;

    public MushafPageView(MushafPageViewModel viewModel, IQuranPageLayout layout)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _layout = layout;
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += (s, e) => RenderLines();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.PageVerses.Count == 0)
        {
            await _viewModel.LoadInitialAsync();
        }
    }

    private void OnJuzPickerChanged(object? sender, EventArgs e)
    {
        if (_viewModel.JumpToJuzCommand.CanExecute(null))
        {
            _viewModel.JumpToJuzCommand.Execute(null);
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

            var verseTuples = verses
                .Select(v => (v.SurahNum, v.AyahNum, v.ArabicText))
                .ToList();
            var lines = _layout.LayoutPage(verseTuples, _viewModel.CurrentPage);

            var isCompact = Width > 0 && Width < 500;
            var effectiveRowHeight = isCompact ? 46.0 : RowHeight;
            var defaultVerseFontSize = 24.0;
            if (Application.Current?.Resources.TryGetValue("FontSizeVerse", out var resObj) == true)
            {
                if (resObj is double d) defaultVerseFontSize = d;
                else if (resObj is OnPlatform<double> op) defaultVerseFontSize = (double)op;
            }
            var effectiveFontSize = isCompact ? 20.0 : defaultVerseFontSize;
            var effectiveLineHeight = 1.8;
            if (Application.Current?.Resources.TryGetValue("LineHeightArabic", out var lhObj) == true && lhObj is double lh)
            {
                effectiveLineHeight = lh;
            }

            for (var lineIdx = 0; lineIdx < RowsPerPage; lineIdx++)
            {
                var line = lineIdx < lines.Count ? lines[lineIdx] : null;
                var lineText = line?.Text ?? string.Empty;
                var lineKey = line?.VerseKey ?? string.Empty;
                var isHighlighted = !string.IsNullOrEmpty(lineKey)
                    && string.Equals(lineKey, _viewModel.HighlightedVerseKey, StringComparison.Ordinal);

                var label = new Label
                {
                    Text = lineText,
                    FontFamily = (string)Application.Current!.Resources["ArabicFontFamily"],
                    FontSize = effectiveFontSize,
                    LineHeight = effectiveLineHeight,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    LineBreakMode = LineBreakMode.NoWrap,
                    MaximumWidthRequest = 680,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Thickness(4, 0),
                    HeightRequest = effectiveRowHeight,
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
                    HeightRequest = effectiveRowHeight
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
