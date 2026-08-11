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
        LoginGrid.ColumnDefinitions = compact
            ? new ColumnDefinitionCollection { new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) };
        LoginGrid.RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) };
        LoginFieldsGrid.ColumnDefinitions = compact
            ? new ColumnDefinitionCollection { new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) };
        LoginFieldsGrid.RowDefinitions = compact
            ? new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) }
            : new RowDefinitionCollection { new(GridLength.Auto) };
        Grid.SetColumn(EmailFieldLayout, 0);
        Grid.SetRow(EmailFieldLayout, 0);
        Grid.SetColumn(PasswordFieldLayout, compact ? 0 : 1);
        Grid.SetRow(PasswordFieldLayout, compact ? 1 : 0);
        Grid.SetRow(OfflineStatusLayout, compact ? 1 : 0);
        Grid.SetColumn(OfflineStatusLayout, compact ? 0 : 1);
        LoginActionsLayout.HorizontalOptions = compact ? LayoutOptions.Fill : LayoutOptions.Start;
    }
}
