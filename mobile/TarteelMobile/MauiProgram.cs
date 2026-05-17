using Microsoft.Extensions.Logging;
using TarteelMobile.Services;
using TarteelMobile.ViewModels;
using TarteelMobile.Views;

namespace TarteelMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMaui()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf",    "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf",   "OpenSansSemibold");
                fonts.AddFont("NotoNaskhArabic-Regular.ttf", "ArabicRegular");
            });

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<IApiService, ApiService>();
        builder.Services.AddSingleton<IRecitationService, RecitationService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<RecitationViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<LoginViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        builder.Services.AddTransient<RecitationPage>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<LoginPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
