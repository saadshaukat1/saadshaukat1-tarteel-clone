using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http.Json;
using TarteelClone.QuranEngine.Services;

namespace TarteelClone.Api.Hubs;

/// <summary>
/// SignalR hub that streams audio chunks from the client to the ASR service
/// and returns real-time verse matching results.
/// </summary>
[Authorize]
public class RecitationHub : Hub
{
    private readonly IVerseMatchingService _matching;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public RecitationHub(IVerseMatchingService matching,
        IHttpClientFactory httpClientFactory,
        IConfiguration config)
    {
        _matching          = matching;
        _httpClientFactory = httpClientFactory;
        _config            = config;
    }

    /// <summary>
    /// Called by the client for each audio chunk (base-64 encoded PCM/WAV bytes).
    /// The hub forwards the chunk to the Python ASR service, receives Arabic text,
    /// then runs verse matching and pushes the result back.
    /// </summary>
    public async Task SendAudioChunk(string base64AudioChunk)
    {
        var asrBaseUrl = _config["ASRService:BaseUrl"]
            ?? throw new InvalidOperationException("ASRService:BaseUrl not configured.");

        byte[] audioBytes = Convert.FromBase64String(base64AudioChunk);

        // ── Forward to Python ASR microservice ─────────────────────────────────
        var client = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(audioBytes), "audio", "chunk.wav");

        var asrResponse = await client.PostAsync($"{asrBaseUrl}/transcribe", content);
        asrResponse.EnsureSuccessStatusCode();

        var transcription = await asrResponse.Content.ReadFromJsonAsync<AsrResult>();
        if (transcription is null || string.IsNullOrWhiteSpace(transcription.Text))
            return;

        // ── Verse matching ─────────────────────────────────────────────────────
        var matchResult = await _matching.MatchAsync(transcription.Text,
            Context.ConnectionAborted);

        // ── Push result back to the specific caller ────────────────────────────
        await Clients.Caller.SendAsync("ReceiveMatchResult", matchResult);
    }

    private record AsrResult(string Text, double Confidence);
}
