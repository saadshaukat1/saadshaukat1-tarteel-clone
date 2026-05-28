using TarteelMobile.Services;

namespace TarteelMobile;

public partial class App : Application
{
    private readonly IOfflineReadinessService _readiness;
    private readonly IAppDiagnosticsService _diagnostics;

    public App(
        IOfflineReadinessService readiness,
        IAppDiagnosticsService diagnostics)
    {
        _readiness = readiness;
        _diagnostics = diagnostics;

        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        _ = RunStartupReadinessAsync(window);
        return window;
    }

    private async Task RunStartupReadinessAsync(Window window)
    {
        try
        {
            var report = await _readiness.RunStartupChecksAsync();
            _diagnostics.Info($"Startup readiness: {report.Summary}");

            if (!report.IsReady)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (window.Page is Page page)
                    {
                        await page.DisplayAlertAsync(
                            "Offline setup incomplete",
                            report.Summary,
                            "OK");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Failed to run startup readiness checks.", ex);
        }
    }
}
