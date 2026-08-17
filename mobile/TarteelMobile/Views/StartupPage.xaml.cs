using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;

    public StartupPage(StartupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // NOTE: Shell.SetBackButtonBehavior is only valid inside a Shell host.
        // StartupPage lives in a NavigationPage, so we suppress the back button
        // using NavigationPage.SetHasBackButton instead.
        NavigationPage.SetHasBackButton(this, false);

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // async void cannot propagate exceptions — surface them in the UI.
            _viewModel.StatusText = $"Fatal startup error: {ex.Message}";
            _viewModel.ShowRetryButton = true;
            _viewModel.IsIndeterminate = false;
        }
    }
}
