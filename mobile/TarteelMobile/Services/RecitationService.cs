using TarteelMobile.Models;
using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services;

public interface IRecitationService
{
    event EventHandler<MatchResult>? MatchResultReceived;
    bool RequiresAuthentication { get; }
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SendAudioChunkAsync(byte[] audioChunk);
}

public sealed class RecitationService : IRecitationService
{
    private readonly CoreAbstractions.IRecitationOrchestrator _orchestrator;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly object _sync = new();
    private bool _connected;

    public event EventHandler<MatchResult>? MatchResultReceived;
    // Offline desktop mode should not block recitation behind login.
    public bool RequiresAuthentication => false;

    public RecitationService(
        CoreAbstractions.IRecitationOrchestrator orchestrator,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _orchestrator = orchestrator;
        _session = session;
        _diagnostics = diagnostics;
        _orchestrator.MatchProduced += OnCoreMatchProduced;
    }

    public async Task ConnectAsync()
    {
        if (RequiresAuthentication && !_session.IsAuthenticated)
        {
            _diagnostics.Warn("Recitation connect requested without authenticated local session.");
            throw new InvalidOperationException("Please log in before starting recitation.");
        }

        lock (_sync)
        {
            _connected = true;
        }

        await _orchestrator.StartAsync();
        _diagnostics.Info($"Recitation auth requirement: {RequiresAuthentication}.");
        _diagnostics.Info("Recitation pipeline connected in local in-process mode.");
    }

    public Task DisconnectAsync()
    {
        lock (_sync)
        {
            _connected = false;
        }

        _diagnostics.Info("Recitation pipeline disconnected.");
        return _orchestrator.StopAsync();
    }

    public async Task SendAudioChunkAsync(byte[] audioChunk)
    {
        if (audioChunk is null || audioChunk.Length == 0)
        {
            return;
        }

        bool isConnected;
        lock (_sync)
        {
            isConnected = _connected;
        }

        if (!isConnected)
            return;

        await _orchestrator.SubmitAudioChunkAsync(audioChunk);
    }

    private void OnCoreMatchProduced(object? sender, CoreModels.RecitationMatchResult result)
    {
        MatchResultReceived?.Invoke(this, new MatchResult
        {
            SurahNum = result.SurahNum,
            AyahNum = result.AyahNum,
            ArabicText = result.ArabicText,
            Confidence = result.Confidence,
            Mismatches = result.Mismatches
                .Select(m => new WordMismatch(m.Position, m.Spoken, m.Expected))
                .ToList()
        });
    }
}
