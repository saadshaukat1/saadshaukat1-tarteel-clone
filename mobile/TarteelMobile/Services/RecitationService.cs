using TarteelMobile.Models;
using CoreAbstractions = TarteelClone.LocalRecitationCore.Abstractions;
using CoreModels = TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services;

public interface IRecitationService
{
    event EventHandler<MatchResult>? MatchResultReceived;
    event EventHandler<string>? DiagnosticEmitted;
    bool RequiresAuthentication { get; }
    void SetSurahContext(int surahNum, string surahArabicName);
    void ClearSurahContext();
    Task ConnectAsync();
    Task FlushAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendAudioChunkAsync(byte[] audioChunk);
}

public sealed class RecitationService : IRecitationService
{
    private readonly CoreAbstractions.IRecitationOrchestrator _orchestrator;
    private readonly CoreAbstractions.IAsrEngine _asrEngine;
    private readonly ISessionService _session;
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _chunkProcessingGate = new(1, 1);
    private CancellationTokenSource? _sessionCancellation;
    private bool _connected;

    public event EventHandler<MatchResult>? MatchResultReceived;
    public event EventHandler<string>? DiagnosticEmitted;
    public bool RequiresAuthentication => false;

    public RecitationService(
        CoreAbstractions.IRecitationOrchestrator orchestrator,
        CoreAbstractions.IAsrEngine asrEngine,
        ISessionService session,
        IAppDiagnosticsService diagnostics)
    {
        _orchestrator = orchestrator;
        _asrEngine = asrEngine;
        _session = session;
        _diagnostics = diagnostics;
        _orchestrator.MatchProduced += OnCoreMatchProduced;
        _orchestrator.DiagnosticEmitted += OnCoreDiagnosticEmitted;
    }

    public void SetSurahContext(int surahNum, string surahArabicName)
    {
        _orchestrator.SetSurahContext(surahNum);
        _asrEngine.SetSurahPrompt(surahArabicName);
    }

    public void ClearSurahContext()
    {
        _orchestrator.ClearSurahContext();
        _asrEngine.ClearSurahPrompt();
    }

    public async Task ConnectAsync()
    {
        if (RequiresAuthentication && !_session.IsAuthenticated)
        {
            _diagnostics.Warn("Recitation connect requested without authenticated local session.");
            throw new InvalidOperationException("Please log in before starting recitation.");
        }

        CancellationTokenSource? previousSessionCancellation;
        CancellationToken sessionToken;
        lock (_sync)
        {
            previousSessionCancellation = _sessionCancellation;
            _sessionCancellation = new CancellationTokenSource();
            sessionToken = _sessionCancellation.Token;
            _connected = true;
        }

        previousSessionCancellation?.Cancel();
        previousSessionCancellation?.Dispose();

        try
        {
            await _orchestrator.StartAsync(sessionToken);
        }
        catch
        {
            CancellationTokenSource? failedSessionCancellation;
            lock (_sync)
            {
                _connected = false;
                failedSessionCancellation = _sessionCancellation;
                _sessionCancellation = null;
            }

            failedSessionCancellation?.Cancel();
            failedSessionCancellation?.Dispose();
            throw;
        }

        _diagnostics.Info($"Recitation auth requirement: {RequiresAuthentication}.");
        _diagnostics.Info("Recitation pipeline connected in local in-process mode.");
    }

    public async Task DisconnectAsync()
    {
        CancellationTokenSource? currentSessionCancellation;
        lock (_sync)
        {
            _connected = false;
            currentSessionCancellation = _sessionCancellation;
            _sessionCancellation = null;
        }

        currentSessionCancellation?.Cancel();
        _diagnostics.Info("Recitation pipeline disconnected.");
        try
        {
            await _orchestrator.StopAsync();
        }
        finally
        {
            currentSessionCancellation?.Dispose();
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _chunkProcessingGate.WaitAsync(cancellationToken);
        _chunkProcessingGate.Release();
    }

    public async Task SendAudioChunkAsync(byte[] audioChunk)
    {
        if (audioChunk is null || audioChunk.Length == 0)
        {
            return;
        }

        bool isConnected;
        CancellationToken sessionToken;
        lock (_sync)
        {
            isConnected = _connected;
            sessionToken = _sessionCancellation?.Token ?? CancellationToken.None;
        }

        if (!isConnected)
            return;

        bool acquired = false;
        try
        {
            await _chunkProcessingGate.WaitAsync(sessionToken);
            acquired = true;
            lock (_sync)
            {
                if (!_connected)
                {
                    return;
                }

                sessionToken = _sessionCancellation?.Token ?? CancellationToken.None;
            }

            if (AudioPreprocessor.IsSilence(audioChunk))
                return;

            var normalizedChunk = AudioPreprocessor.NormalizeRms(audioChunk);
            await _orchestrator.SubmitAudioChunkAsync(normalizedChunk, sessionToken);
        }
        catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (acquired && _chunkProcessingGate.CurrentCount == 0)
            {
                _chunkProcessingGate.Release();
            }
        }
    }

    private void OnCoreMatchProduced(object? sender, CoreModels.RecitationMatchResult result)
    {
        MatchResultReceived?.Invoke(this, new MatchResult
        {
            SurahNum = result.SurahNum,
            AyahNum = result.AyahNum,
            ArabicText = result.ArabicText,
            TranscriptionText = result.TranscriptionText,
            Confidence = result.Confidence,
            ProcessedWordCount = result.ProcessedWordCount,
            MatchedWordCount = result.MatchedWordCount,
            JuzNum = result.JuzNum,
            SurahNameEnglish = result.SurahNameEnglish,
            SurahNameArabic = result.SurahNameArabic,
            Mismatches = result.Mismatches
                .Select(m => new WordMismatch(m.Position, m.Spoken, m.Expected))
                .ToList(),
            TajweedViolations = result.TajweedViolations
                .Select(v => new TajweedViolation(
                    v.WordPosition, v.Rule, v.ExpectedWord, v.SpokenWord, v.Hint, v.RuleDisplayName))
                .ToList()
        });
    }

    private void OnCoreDiagnosticEmitted(object? sender, string message)
    {
        DiagnosticEmitted?.Invoke(this, message);
    }
}
