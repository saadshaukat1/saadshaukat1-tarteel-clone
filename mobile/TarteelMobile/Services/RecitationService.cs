using Microsoft.AspNetCore.SignalR.Client;
using TarteelMobile.Models;

namespace TarteelMobile.Services;

public interface IRecitationService
{
    event EventHandler<MatchResult>? MatchResultReceived;
    Task ConnectAsync(string token);
    Task DisconnectAsync();
    Task SendAudioChunkAsync(byte[] audioChunk);
}

public class RecitationService : IRecitationService, IAsyncDisposable
{
    private HubConnection? _hub;
    private const string HubUrl = "https://localhost:7001/hubs/recitation";

    public event EventHandler<MatchResult>? MatchResultReceived;

    public async Task ConnectAsync(string token)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{HubUrl}?access_token={token}")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<MatchResult>("ReceiveMatchResult", result =>
            MatchResultReceived?.Invoke(this, result));

        await _hub.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
            await _hub.StopAsync();
    }

    public async Task SendAudioChunkAsync(byte[] audioChunk)
    {
        if (_hub?.State != HubConnectionState.Connected)
            return;

        var base64 = Convert.ToBase64String(audioChunk);
        await _hub.InvokeAsync("SendAudioChunk", base64);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
            await _hub.DisposeAsync();
    }
}
