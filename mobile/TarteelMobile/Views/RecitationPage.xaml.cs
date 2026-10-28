using Microsoft.Maui.Controls;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class RecitationPage : ContentPage
{
    private readonly RecitationViewModel _vm;

    public RecitationPage(RecitationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

        _vm.RequestCanvasRedraw += OnRequestCanvasRedraw;

        // Desktop keyboard shortcuts: Space toggles the mic, Escape ends the session.
#if WINDOWS
        Loaded += OnRecitationLoaded;
#endif
    }

    private void OnRequestCanvasRedraw()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MushafView?.Invalidate();
        });
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (MushafView.Width > 50 && MushafView.Height > 50)
        {
            _vm.InitializePageLayout((float)MushafView.Width, (float)MushafView.Height);
            MushafView.Invalidate();
        }
    }

#if WINDOWS
    private void OnRecitationLoaded(object? sender, EventArgs e)
    {
        if (this.Content?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement root)
        {
            root.KeyDown += OnRecitationKeyDown;
        }
    }

    private void OnRecitationKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Space)
        {
            if (_vm.ToggleRecordingCommand.CanExecute(null))
            {
                _vm.ToggleRecordingCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_vm.StopRecitationCommand.CanExecute(null))
            {
                _vm.StopRecitationCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshAuthState();
        MushafView?.Invalidate();
    }
}
