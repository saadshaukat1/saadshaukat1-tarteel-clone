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
        MainPage = new AppShell();
        _ = RunStartupReadinessAsync();
    }

    private async Task RunStartupReadinessAsync()
    {
        try
        {
            var report = await _readiness.RunStartupChecksAsync();
            _diagnostics.Info($"Startup readiness: {report.Summary}");
            if (!report.IsReady)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (Current?.MainPage is not null)
                    {
                        await Current.MainPage.DisplayAlert(
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
