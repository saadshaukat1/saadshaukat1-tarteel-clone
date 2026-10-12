using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TarteelClone.QuranEngine;
using TarteelMobile.Models;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class MushafPageView : ContentPage
{
    private const int RowsPerPage = 16;

    private readonly MushafPageViewModel _viewModel;
    private readonly IQuranPageLayout _layout;
    private bool _isRendering;

    public MushafPageView(MushafPageViewModel viewModel, IQuranPageLayout layout)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _layout = layout;
        BindingContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += (_, _) => RenderLines();
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
            or nameof(MushafPageViewModel.HighlightedVerseKey)
            or nameof(MushafPageViewModel.ArabicFontSize))
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

            var availableHeight = PageScroll.Height > 0 ? PageScroll.Height : 680;
            var lineHeight = Math.Max(38.0, (availableHeight - 24) / RowsPerPage);
            var effectiveFontSize = _viewModel.ArabicFontSize;
            var effectiveLineHeight = 1.7;

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
                    HeightRequest = Math.Max(lineHeight, effectiveFontSize * effectiveLineHeight),
                    TextColor = (Color)Application.Current.Resources["MushafInk"],
                    FontAttributes = isHighlighted ? FontAttributes.Bold : FontAttributes.None,
                    AutomationId = string.IsNullOrEmpty(lineKey)
                        ? $"mushaf-line-{lineIdx}"
                        : $"mushaf-verse-{lineKey}-line-{lineIdx}"
                };

                SemanticProperties.SetDescription(label, isHighlighted
                    ? $"Current recitation verse, line {lineIdx + 1}: {lineText}"
                    : $"Mushaf line {lineIdx + 1}: {lineText}");

                var cell = new Border
                {
                    BackgroundColor = (Color)Application.Current.Resources["MushafPaper"],
                    Stroke = isHighlighted
                        ? (Color)Application.Current.Resources["AccentMuted"]
                        : Colors.Transparent,
                    StrokeThickness = isHighlighted ? 1 : 0,
                    Content = label,
                    HeightRequest = Math.Max(lineHeight, effectiveFontSize * effectiveLineHeight)
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
}
