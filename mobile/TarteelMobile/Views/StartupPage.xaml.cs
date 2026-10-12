using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel _viewModel;
    private bool _hasStartedInitialization;

    public StartupPage(StartupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        StartInitializationOnce();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartInitializationOnce();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // NOTE: Shell.SetBackButtonBehavior is only valid inside a Shell host.
        // StartupPage lives in a NavigationPage, so we suppress the back button
        // using NavigationPage.SetHasBackButton instead.
        NavigationPage.SetHasBackButton(this, false);

        StartInitializationOnce();
    }

    private async void StartInitializationOnce()
    {
        if (_hasStartedInitialization)
            return;

        _hasStartedInitialization = true;

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
