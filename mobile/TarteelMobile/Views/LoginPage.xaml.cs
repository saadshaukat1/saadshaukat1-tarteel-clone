using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        var compact = Width > 0 && Width < 760;
        LoginGrid.Padding = compact ? new Thickness(20, 20, 20, 28) : new Thickness(44, 28, 44, 36);
        LoginFormLayout.HorizontalOptions = compact ? LayoutOptions.Fill : LayoutOptions.Center;
    }
}
