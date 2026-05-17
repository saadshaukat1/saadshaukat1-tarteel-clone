using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TarteelMobile.Models;
using TarteelMobile.Services;

namespace TarteelMobile.ViewModels;

/// <summary>
/// Drives the real-time recitation screen:
/// - Starts/stops mic recording
/// - Streams audio chunks through the SignalR hub
/// - Shows the matched verse with highlighted mismatches
/// </summary>
public partial class RecitationViewModel : ObservableObject
{
    private readonly IAudioService      _audio;
    private readonly IRecitationService _recitation;
    private readonly IApiService        _api;

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
        IRecitationService recitation, IApiService api)
    {
        _audio      = audio;
        _recitation = recitation;
        _api        = api;

        _audio.AudioChunkReady        += OnAudioChunkReady;
        _recitation.MatchResultReceived += OnMatchResultReceived;
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await _audio.StopRecordingAsync();
            IsRecording    = false;
            StatusMessage  = "Recitation paused";
        }
        else
        {
            if (_api.AuthToken is null)
            {
                StatusMessage = "Please log in first";
                return;
            }

            await _recitation.ConnectAsync(_api.AuthToken);
            await _audio.StartRecordingAsync();
            IsRecording   = true;
            StatusMessage = "Listening…";
        }
    }

    private async void OnAudioChunkReady(object? sender, byte[] chunk)
        => await _recitation.SendAudioChunkAsync(chunk);

    private void OnMatchResultReceived(object? sender, MatchResult result)
    {
        ArabicText    = result.ArabicText;
        Confidence    = result.Confidence;
        Mismatches    = result.Mismatches;
        StatusMessage = result.Mismatches.Count == 0
            ? $"✓ Surah {result.SurahNum}:{result.AyahNum} — perfect!"
            : $"Surah {result.SurahNum}:{result.AyahNum} — {result.Mismatches.Count} correction(s)";
    }
}
