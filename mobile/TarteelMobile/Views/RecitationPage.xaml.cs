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

        // Desktop keyboard shortcuts: Space toggles the mic, Escape ends the
        // session. MAUI 9 has no cross-platform Page.KeyDown, so on Windows we
        // hook the WinUI root content's KeyDown event.
#if WINDOWS
        Loaded += OnRecitationLoaded;
#endif
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
            if (_vm.StopSessionCommand.CanExecute(null))
            {
                _vm.StopSessionCommand.Execute(null);
            }

            e.Handled = true;
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshAuthState();
    }
}
