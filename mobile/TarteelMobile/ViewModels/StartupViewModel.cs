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
    private readonly ISessionService _sessionService;

#pragma warning disable MVVMTK0045
    [ObservableProperty]
    private string _statusText = "Starting up...";

    [ObservableProperty]
    private double _progress = 0;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private bool _showRetryButton = false;
#pragma warning restore MVVMTK0045

    public StartupViewModel(
        IOfflineReadinessService readinessService,
        IAsrEngine asrEngine,
        IAppDiagnosticsService diagnostics,
        ISessionService sessionService)
    {
        _readinessService = readinessService;
        _asrEngine = asrEngine;
        _diagnostics = diagnostics;
        _sessionService = sessionService;
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
        StatusText = "Preparing offline recitation engine...";
        _diagnostics.Info("StartupViewModel: InitializeAsync started.");

        _asrEngine.DownloadProgressChanged += OnDownloadProgressChanged;

        try
        {
            var report = await _readinessService.RunStartupChecksAsync();
            _diagnostics.Info($"StartupViewModel: Startup readiness checks completed. IsReady={report.IsReady}");

            if (report.IsReady)
            {
                StatusText = "Offline Engine Ready!";
                await Task.Delay(500); // Give user a moment to see it

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        var targetPage = _sessionService.IsAuthenticated
                            ? (Page)IPlatformApplication.Current!.Services.GetRequiredService<AppShell>()
                            : (Page)IPlatformApplication.Current!.Services.GetRequiredService<Views.LoginPage>();

                        _diagnostics.Info($"StartupViewModel: Navigating to {(targetPage is AppShell ? "AppShell" : "LoginPage")}.");

                        if (Application.Current?.Windows.Count > 0)
                        {
                            Application.Current.Windows[0].Page = targetPage;
                        }
                        else if (Application.Current != null)
                        {
                            Application.Current.OpenWindow(new Window(targetPage));
                        }
                    }
                    catch (Exception navEx)
                    {
                        _diagnostics.Error("StartupViewModel: Failed to navigate to main/login page.", navEx);
                        StatusText = $"Navigation failed: {navEx.Message}";
                        ShowRetryButton = true;
                        IsIndeterminate = false;
                    }
                });
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
            StatusText = $"Setup error: {ex.Message}";
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
