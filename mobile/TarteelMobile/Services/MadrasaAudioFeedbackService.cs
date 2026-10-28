using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;

namespace TarteelMobile.Services;

public interface IMadrasaAudioFeedbackService
{
    Task PlayTeacherKnockAlertAsync();
    Task SpeakLuqmahPromptAsync(string wordOrPhrase, CancellationToken cancellationToken = default);
    Task SpeakTeacherVerdictAsync(string verdictArabic, CancellationToken cancellationToken = default);
}

public sealed class MadrasaAudioFeedbackService : IMadrasaAudioFeedbackService
{
    private readonly IAppDiagnosticsService _diagnostics;

    public MadrasaAudioFeedbackService(IAppDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task PlayTeacherKnockAlertAsync()
    {
        try
        {
#if WINDOWS
            System.Media.SystemSounds.Asterisk.Play();
#else
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch
            {
                // Ignore haptic failures
            }
#endif
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"Could not play teacher knock alert: {ex.Message}");
        }
    }

    public async Task SpeakLuqmahPromptAsync(string wordOrPhrase, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wordOrPhrase)) return;

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var arabicLocale = locales.FirstOrDefault(l => l.Language.StartsWith("ar", StringComparison.OrdinalIgnoreCase));

            var options = new SpeechOptions
            {
                Locale = arabicLocale,
                Pitch = 1.0f,
                Volume = 1.0f
            };

            await TextToSpeech.Default.SpeakAsync(wordOrPhrase, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"TextToSpeech Luqmah failed: {ex.Message}");
        }
    }

    public async Task SpeakTeacherVerdictAsync(string verdictArabic, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verdictArabic)) return;

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var arabicLocale = locales.FirstOrDefault(l => l.Language.StartsWith("ar", StringComparison.OrdinalIgnoreCase));

            var options = new SpeechOptions
            {
                Locale = arabicLocale,
                Pitch = 0.95f,
                Volume = 1.0f
            };

            await TextToSpeech.Default.SpeakAsync(verdictArabic, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn($"TextToSpeech Teacher Verdict failed: {ex.Message}");
        }
    }
}
