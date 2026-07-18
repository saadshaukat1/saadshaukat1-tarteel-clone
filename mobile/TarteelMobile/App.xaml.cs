using System.Text;
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

        // Wire up global crash logging before anything else.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

#if WINDOWS
        // WinUI XAML parse errors bypass AppDomain.UnhandledException.
        // Hook the native WinUI handler to capture those crashes.
        Microsoft.UI.Xaml.Application.Current.UnhandledException += OnWinUIUnhandledException;
#endif

#if ANDROID
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            _diagnostics.Error("Android UnhandledException", args.Exception);
            CrashLog(args.Exception, "Android UnhandledException");
            args.Handled = true;
        };
#endif

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            diagnostics.Error("InitializeComponent failed", ex);
            CrashLog(ex, "InitializeComponent");
            throw;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        try
        {
            var window = new Window(new AppShell());
            _ = RunStartupReadinessAsync(window);
            return window;
        }
        catch (Exception ex)
        {
            _diagnostics.Error("CreateWindow failed", ex);
            CrashLog(ex, "CreateWindow");
            throw;
        }
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _diagnostics.Error($"UnhandledException (terminating={e.IsTerminating})", ex);
        CrashLog(ex, $"UnhandledException terminating={e.IsTerminating}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _diagnostics.Error("UnobservedTaskException", e.Exception);
        CrashLog(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }

#if WINDOWS
    private void OnWinUIUnhandledException(object? sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _diagnostics.Error($"WinUI UnhandledException (handled={e.Handled})", e.Exception);
        CrashLog(e.Exception, $"WinUI UnhandledException handled={e.Handled}");
    }
#endif

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
                        await page.DisplayAlert(
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

    /// <summary>
    /// Writes crash details to a standalone file as a fallback when the
    /// diagnostics service isn't available yet or itself fails.
    /// </summary>
    private static void CrashLog(Exception? ex, string context)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TarteelClone",
                "diagnostics");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var sb = new StringBuilder();
            sb.AppendLine($"--- {DateTimeOffset.UtcNow:u} CRASH [{context}] ---");
            var current = ex;
            while (current is not null)
            {
                sb.AppendLine($"  {current.GetType().FullName}: {current.Message}");
                if (current.StackTrace is { Length: > 0 } st)
                    sb.AppendLine($"  StackTrace: {st}");
                current = current.InnerException;
            }
            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Last resort — nothing we can do.
        }
    }
}
