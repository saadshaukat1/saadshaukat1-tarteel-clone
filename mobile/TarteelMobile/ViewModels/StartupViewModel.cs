using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Services;
using TarteelMobile.Services.Asr;

namespace TarteelMobile.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly IOfflineReadinessService _readinessService;
    private readonly IAsrEngine _asrEngine;
    private readonly IAppDiagnosticsService _diagnostics;

    [ObservableProperty]
    private string _statusText = "Starting up...";

    [ObservableProperty]
    private double _progress = 0;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private bool _showRetryButton = false;

    public StartupViewModel(
        IOfflineReadinessService readinessService,
        IAsrEngine asrEngine,
        IAppDiagnosticsService diagnostics)
    {
        _readinessService = readinessService;
        _asrEngine = asrEngine;
        _diagnostics = diagnostics;
    }

    [RelayCommand]
    public async Task RetryAsync()
    {
        await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        ShowRetryButton = false;
        IsIndeterminate = true;
        StatusText = "Checking local files...";

        _asrEngine.DownloadProgressChanged += OnDownloadProgressChanged;

        try
        {
            var report = await _readinessService.RunStartupChecksAsync();

            if (report.IsReady)
            {
                StatusText = "Ready!";
                await Task.Delay(500); // Give user a moment to see it
                // Resolve AppShell from DI rather than calling `new AppShell()` directly.
                // This is the correct MAUI pattern and avoids bypassing the service container.
                var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
                Application.Current!.Windows[0].Page = shell;
            }
            else
            {
                StatusText = report.Summary;
                ShowRetryButton = true;
                IsIndeterminate = false;
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Startup check failed", ex);
            StatusText = $"Error: {ex.Message}";
            ShowRetryButton = true;
            IsIndeterminate = false;
        }
        finally
        {
            _asrEngine.DownloadProgressChanged -= OnDownloadProgressChanged;
        }
    }

    private void OnDownloadProgressChanged(object? sender, AsrDownloadProgress e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = e.StatusMessage;
            
            if (e.Fraction >= 0)
            {
                IsIndeterminate = false;
                Progress = e.Fraction;
            }
            else
            {
                IsIndeterminate = true;
            }
        });
    }
}
