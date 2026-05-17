using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
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
    private readonly string _hubUrl;

    public event EventHandler<MatchResult>? MatchResultReceived;

    public RecitationService(IConfiguration config)
    {
        var baseUrl = config["ApiService:BaseUrl"] ?? "https://localhost:7001";
        _hubUrl = $"{baseUrl}/hubs/recitation";
    }

    public async Task ConnectAsync(string token)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{_hubUrl}?access_token={token}")
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
