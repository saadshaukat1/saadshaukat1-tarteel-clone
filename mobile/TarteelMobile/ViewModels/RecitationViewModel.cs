using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

/// <summary>
/// Drives the real-time recitation screen:
/// - Starts/stops mic recording
/// - Streams audio chunks through local in-process recitation pipeline
/// - Shows the matched verse with highlighted mismatches
/// </summary>
public partial class RecitationViewModel : ObservableObject
{
    private const double MatchConfidenceThreshold = 0.65;
    private const double PerfectConfidenceThreshold = 0.90;

    private readonly IAudioService      _audio;
    private readonly IRecitationService _recitation;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;

    [ObservableProperty]
    private string _arabicText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Tap the mic to start reciting";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private List<WordMismatch> _mismatches = [];

    public RecitationViewModel(IAudioService audio,
        IRecitationService recitation,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _audio      = audio;
        _recitation = recitation;
        _session = session;
        _diagnostics = diagnostics;

        _audio.AudioChunkReady        += OnAudioChunkReady;
        _audio.RecordingError         += OnAudioRecordingError;
        _recitation.MatchResultReceived += OnMatchResultReceived;
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (_recitation.RequiresAuthentication && !_session.IsAuthenticated)
        {
            StatusMessage = "Please log in first";
            return;
        }

        if (IsRecording)
        {
            await _audio.StopRecordingAsync();
            await _recitation.DisconnectAsync();
            IsRecording    = false;
            StatusMessage  = "Recitation paused";
            _diagnostics.Info("Recitation recording paused.");
        }
        else
        {
            await _recitation.ConnectAsync();
            await _audio.StartRecordingAsync();
            IsRecording   = true;
            StatusMessage = "Listening…";
            _diagnostics.Info("Recitation recording started.");
        }
    }

    private async void OnAudioChunkReady(object? sender, byte[] chunk)
        => await _recitation.SendAudioChunkAsync(chunk);

    private void OnAudioRecordingError(object? sender, Exception exception)
    {
        IsRecording = false;
        StatusMessage = $"Mic error: {exception.Message}";
        _diagnostics.Error("Audio recording error received in view-model.", exception);
    }

    private void OnMatchResultReceived(object? sender, MatchResult result)
    {
        ArabicText    = result.ArabicText;
        Confidence    = result.Confidence;
        Mismatches    = result.Mismatches;

        if (result.Confidence < MatchConfidenceThreshold)
        {
            StatusMessage = "Listening… keep reciting";
            return;
        }

        if (result.Mismatches.Count == 0 && result.Confidence >= PerfectConfidenceThreshold)
        {
            StatusMessage = $"✓ Surah {result.SurahNum}:{result.AyahNum} — perfect!";
            return;
        }

        StatusMessage = result.Mismatches.Count == 0
            ? $"Surah {result.SurahNum}:{result.AyahNum} — match improving ({result.Confidence:P0})"
            : $"Surah {result.SurahNum}:{result.AyahNum} — {result.Mismatches.Count} correction(s)";
    }

    [RelayCommand]
    private async Task StopSessionAsync()
    {
        await _audio.StopRecordingAsync();
        await _recitation.DisconnectAsync();
        if (_session.IsAuthenticated)
        {
            await _session.LogoutAsync();
        }

        IsRecording = false;
        ArabicText = string.Empty;
        Confidence = 0;
        Mismatches = [];
        StatusMessage = "Session reset. Tap Start to recite again.";
        _diagnostics.Info("Local recitation session reset from recitation screen.");
    }
}
