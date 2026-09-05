using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TarteelClone.QuranEngine;
using TarteelMobile.Services;
using TarteelMobile.Services.Asr;
using TarteelMobile.Services.Core;
using TarteelMobile.ViewModels;
using TarteelMobile.Views;
using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreServices = TarteelClone.LocalRecitationCore.Services;
using ILineLevelPageSource = TarteelClone.QuranEngine.ILineLevelPageSource;
using JsonLinePageSource = TarteelClone.QuranEngine.JsonLinePageSource;

namespace TarteelMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("NotoNaskhArabic.ttf", "NotoNaskhArabic");
                fonts.AddFont("Nastaleeq.ttf", "Nastaleeq");
            });

        ConfigureAppConfiguration(builder);

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<IAppDiagnosticsService, FileAppDiagnosticsService>();
        builder.Services.AddSingleton<ISessionService, LocalSessionService>();
        builder.Services.AddSingleton<IOptions<LocalQuranDataOptions>>(sp =>
        {
            var options = new LocalQuranDataOptions();
            BindOptions(sp.GetRequiredService<IConfiguration>(), LocalQuranDataOptions.SectionName, options);
            return Options.Create(options);
        });
        builder.Services.AddSingleton<TarteelMobile.Services.IVerseRepository, LocalVerseRepository>();
        builder.Services.AddSingleton<ITodayWorkflowService, TodayWorkflowService>();
        builder.Services.AddSingleton<IOfflineReadinessService, OfflineReadinessService>();
        builder.Services.AddSingleton<IOptions<LocalWhisperOptions>>(sp =>
        {
            var options = new LocalWhisperOptions();
            BindOptions(sp.GetRequiredService<IConfiguration>(), LocalWhisperOptions.SectionName, options);
            return Options.Create(options);
        });
        builder.Services.AddSingleton<IAsrEngine, LocalWhisperAsrEngine>();
        builder.Services.AddSingleton<CoreAbstractions.IAsrEngine, AsrEngineCoreAdapter>();
        builder.Services.AddSingleton<CoreAbstractions.IVerseRepository, VerseRepositoryCoreAdapter>();
        builder.Services.AddSingleton<CoreAbstractions.IVerseMatcher, CoreServices.LocalVerseMatcher>();
        builder.Services.AddSingleton<CoreAbstractions.IProgressStore, DiagnosticsProgressStore>();
        builder.Services.AddSingleton<CoreAbstractions.IRecitationOrchestrator, CoreServices.OfflineRecitationOrchestrator>();
        builder.Services.AddSingleton<IRecitationService, RecitationService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        builder.Services.AddSingleton<IQuranPageLayout, Mushaf16LinerLayout>();
        builder.Services.AddSingleton<ICurriculumService, CurriculumService>();
        builder.Services.AddSingleton<TarteelClone.UserService.IUserProfileStore, SqliteUserProfileStore>();
        builder.Services.AddSingleton<TarteelClone.SearchService.IVerseSearchIndex, TarteelClone.SearchService.WordMatchSearchIndex>();
        builder.Services.AddSingleton<TarteelClone.Api.ILocalApi, LocalApiService>();
        builder.Services.AddSingleton<ILineLevelPageSource, JsonLinePageSource>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<StartupViewModel>();
        builder.Services.AddTransient<Views.StartupPage>();
        
        builder.Services.AddTransient<RecitationViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<MushafPageViewModel>();

        // ── Pages ─────────────────────────────────────────────────────────────
        builder.Services.AddTransient<RecitationPage>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MushafPageView>();
        builder.Services.AddTransient<AppShell>();

        builder.Logging.SetMinimumLevel(LogLevel.Information);

#if DEBUG
        // Surface .NET/MAUI logs in adb logcat during development.
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureAppConfiguration(MauiAppBuilder builder)
    {
        // Run the file I/O on a background thread to avoid blocking the main
        // thread (ANR risk on Android) during app startup.
        try
        {
            var rawJson = Task.Run(async () =>
            {
                await using var stream = await FileSystem.Current
                    .OpenAppPackageFileAsync("appsettings.json");
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }).GetAwaiter().GetResult();

            var root = JsonDocument.Parse(rawJson).RootElement;
            var inMemory = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            FlattenJsonElement(root, inMemory, prefix: string.Empty);

            foreach (var pair in inMemory)
            {
                builder.Configuration[pair.Key] = pair.Value;
            }
        }
        catch
        {
            // Use option defaults when packaged appsettings.json is unavailable.
        }
    }

    private static void BindOptions<TOptions>(IConfiguration configuration, string sectionName, TOptions target)
        where TOptions : class
    {
        var section = configuration.GetSection(sectionName);
        if (section.Value is null && !section.GetChildren().Any())
        {
            return;
        }

        BindObject(section, target);
    }

    private static void BindObject(IConfiguration section, object target)
    {
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var rawValue = section[property.Name];
            if (rawValue is not null)
            {
                TryAssignSimpleValue(target, property, rawValue);
                continue;
            }

            if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                var childSection = section.GetSection(property.Name);
                if (childSection.Value is null && !childSection.GetChildren().Any())
                {
                    continue;
                }

                var nestedTarget = property.GetValue(target) ?? Activator.CreateInstance(property.PropertyType);
                if (nestedTarget is null)
                {
                    continue;
                }

                BindObject(childSection, nestedTarget);
                property.SetValue(target, nestedTarget);
            }
        }
    }

    private static void TryAssignSimpleValue(object target, PropertyInfo property, string rawValue)
    {
        try
        {
            object? converted = property.PropertyType switch
            {
                var t when t == typeof(string) => rawValue,
                var t when t == typeof(bool) => bool.Parse(rawValue),
                var t when t == typeof(int) => int.Parse(rawValue),
                var t when t == typeof(float) => float.Parse(rawValue),
                var t when t == typeof(double) => double.Parse(rawValue),
                var t when t == typeof(long) => long.Parse(rawValue),
                _ => Convert.ChangeType(rawValue, property.PropertyType)
            };

            property.SetValue(target, converted);
        }
        catch
        {
            // Keep defaults if conversion fails.
        }
    }

    private static void FlattenJsonElement(
        JsonElement element,
        IDictionary<string, string?> output,
        string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrWhiteSpace(prefix)
                        ? property.Name
                        : $"{prefix}:{property.Name}";
                    FlattenJsonElement(property.Value, output, key);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJsonElement(item, output, $"{prefix}:{index}");
                    index++;
                }
                break;
            case JsonValueKind.String:
                output[prefix] = element.GetString();
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                output[prefix] = element.ToString();
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                output[prefix] = null;
                break;
            default:
                output[prefix] = element.GetRawText();
                break;
        }
    }
}
