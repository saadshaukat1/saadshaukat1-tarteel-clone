using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TarteelClone.LocalRecitationCore.Models;
using TarteelClone.LocalRecitationCore.Utilities;
using Whisper.net;
using Whisper.net.SamplingStrategy;

namespace TarteelMobile.Services.Asr;

/// <summary>
/// In-process Whisper ASR engine using Whisper.net C# bindings.
/// Models are persisted to FileSystem.AppDataDirectory — survives app restarts.
/// </summary>
public partial class LocalWhisperAsrEngine(
    IOptions<LocalWhisperOptions> options,
    ILogger<LocalWhisperAsrEngine> logger,
    IAppDiagnosticsService diagnostics) : IAsrEngine, IAsyncDisposable
{
    private static readonly HttpClient SharedHttpClient = new();

    private readonly LocalWhisperOptions _options = options.Value;
    private readonly ILogger<LocalWhisperAsrEngine> _logger = logger;
    private readonly IAppDiagnosticsService _diagnostics = diagnostics;
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    // Prevent concurrent download attempts (e.g. InitializeAsync + TranscribeAsync race).
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    // Prevent concurrent model lifecycle operations (initialize/import/reload).
    private readonly SemaphoreSlim _modelLifecycleLock = new(1, 1);

    private WhisperFactory? _activeFactory;
    private string? _activeFactoryModelPath;

    private bool _isInitialized;
    private bool _isUsingMockMode;
    private bool _warmupDone;

    // Cross-chunk context: carry last ~5 words of previous transcription
    // as a prompt so Whisper maintains continuity across audio boundaries.
    private string _crossChunkContext = string.Empty;

    // Surah name injected into Whisper prompt to bias decoding toward Quranic vocabulary.
    private string? _surahPrompt;

    // Adaptive performance profile state (Auto mode).
    private bool _tokenTimestampsLockedOff;
    private bool _timestampsEnabled;
    private bool _profileDecisionLogged;
    private int _rtfSampleCount;
    private double _rtfMovingAverage;

    public bool IsReady => _isInitialized;

    public bool IsModelPresent
    {
        get
        {
            var tier = _options.PrimaryTier.Trim().ToLowerInvariant();
            if (!_options.TryGetTierDefinition(tier, out var tierDef) || tierDef is null)
                return false;
            return File.Exists(ResolvePersistentModelPath(tierDef.ModelPath));
        }
    }

    public string ActiveTier { get; private set; } = string.Empty;
    public bool IsUsingMockMode => _isUsingMockMode;

    public void SetSurahPrompt(string surahArabicName)
    {
        _surahPrompt = surahArabicName;
    }

    public void ClearSurahPrompt()
    {
        _surahPrompt = null;
    }

    public event EventHandler<AsrDownloadProgress>? DownloadProgressChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _modelLifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized && !_isUsingMockMode)
        {
            return;
        }

        var tier = _options.PrimaryTier.Trim().ToLowerInvariant();
        ActiveTier = tier;
        _isUsingMockMode = false;

        if (!_options.Enabled || !_options.PreferLocalEngine)
        {
            _logger.LogInformation(
                "Whisper.net disabled by config. Enabled={Enabled} PreferLocalEngine={PreferLocalEngine}",
                _options.Enabled, _options.PreferLocalEngine);
            _isInitialized = false;
            return;
        }

        if (!_options.TryGetTierDefinition(tier, out var tierDef) || tierDef is null)
        {
            _logger.LogWarning("Unknown model tier '{Tier}'.", tier);
            FallbackToMockOrFail();
            return;
        }

        // Resolve the primary tier's model to persistent writable storage.
        var modelPath = ResolvePersistentModelPath(tierDef.ModelPath);

        var resolvedTier = tier;
        var resolvedPath = modelPath;

        // If the primary model is not on disk, try to obtain it (extract from
        // packaged assets, then download). Emit progress so the UI can show it.
        if (!File.Exists(modelPath))
        {
            _logger.LogInformation(
                "Model not found at '{Path}' for tier '{Tier}' — trying to extract from packaged assets.", modelPath, tier);

            var extract = await TryExtractModelFromAssetsAsync(tier, modelPath, tierDef, cancellationToken);
            if (!extract.IsSuccess)
            {
                _logger.LogInformation(
                    "Model extraction from assets failed for tier '{Tier}': {Error} — falling back to download.",
                    tier, extract.ErrorMessage);

                var download = await TryDownloadModelAsync(tier, modelPath, tierDef, cancellationToken);
                if (!download.IsSuccess)
                {
                    _logger.LogWarning("Model unavailable for tier '{Tier}': {Error}", tier, download.ErrorMessage);

                    // Fall back to a tier whose model file already exists on disk
                    // (e.g. primary 'tiny' missing but 'base' is present) instead of
                    // dropping straight into silent mock mode.
                    var fallback = ResolveUsableFallbackTier(tier);
                    if (fallback is not null)
                    {
                        resolvedTier = fallback.Value.Tier;
                        resolvedPath = fallback.Value.Path;
                        _logger.LogInformation(
                            "Falling back to present tier '{Tier}' at '{Path}'.", resolvedTier, resolvedPath);
                    }
                    else
                    {
                        FallbackToMockOrFail();
                        return;
                    }
                }
            }
        }
        else
        {
            _logger.LogInformation(
                "Model already present at '{Path}' for tier '{Tier}' — skipping download.", modelPath, tier);
        }

        if (!TryBuildFactory(resolvedPath, out var factoryError))
        {
            _logger.LogWarning("Whisper.net factory init failed: {Error}", factoryError);
            FallbackToMockOrFail();
            return;
        }

        ActiveTier = resolvedTier;
        _logger.LogInformation("Whisper.net initialized with tier '{Tier}' from '{Path}'.", ActiveTier, resolvedPath);

        _isInitialized = true;
        _logger.LogInformation("Whisper.net initialized with tier '{Tier}' from '{Path}'.", ActiveTier, modelPath);
        _diagnostics.Info(
            $"Whisper config: language={_options.Language} noSpeechThresh={_options.NoSpeechThreshold} entropyThresh={_options.EntropyThreshold} beamSize={_options.BeamSearchWidth} temp={_options.Temperature} threads={_options.ThreadsOverride} noTimestamps={_options.NoTimestamps}");
        }
        finally
        {
            _modelLifecycleLock.Release();
        }
    }

    public async Task<RecitationTranscriptionResult> TranscribeAsync(
        byte[] audioChunk,
        CancellationToken cancellationToken = default)
    {
        if (audioChunk.Length == 0)
        {
            return RecitationTranscriptionResult.Failure("Audio chunk was empty.", ActiveTier);
        }

        if (_isUsingMockMode)
        {
            _logger.LogDebug("ASR mock mode active — no real transcription.");
            return RecitationTranscriptionResult.Failure(
                "ASR running in mock mode; no real transcription available.", ActiveTier);
        }

        // ✅ Guard: do not attempt transcription if factory was never built.
        // InitializeAsync must succeed before transcription is possible.
        if (_activeFactory is null)
        {
            return RecitationTranscriptionResult.Failure(
                "Whisper factory not ready. Call InitializeAsync first.", ActiveTier);
        }

        var activeTier = ActiveTier.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(activeTier))
        {
            activeTier = _options.PrimaryTier.Trim().ToLowerInvariant();
        }

        var result = await TryTranscribeWithTierAsync(activeTier, audioChunk, false, cancellationToken);
        if (result.IsSuccess)
        {
            return result;
        }

        var fallback = _options.FallbackTier?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(fallback)
            || string.Equals(activeTier, fallback, StringComparison.Ordinal))
        {
            return result;
        }

        // Only retry with the fallback tier when it would actually use a different
        // model file — retrying the same factory can never succeed and only burns
        // another timeout budget.
        if (!_options.TryGetTierDefinition(fallback, out var fallbackDef) || fallbackDef is null)
        {
            return result;
        }

        var fallbackPath = ResolvePersistentModelPath(fallbackDef.ModelPath);
        if (string.Equals(fallbackPath, _activeFactoryModelPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Fallback tier '{Fallback}' resolves to the same model as the active factory ({Path}) — skipping retry.",
                fallback, fallbackPath);
            return result;
        }

        _logger.LogWarning(
            "Active tier '{Active}' failed. Trying fallback '{Fallback}'. Reason: {Reason}",
            activeTier, fallback, result.DiagnosticMessage);

        var fallbackResult = await TryTranscribeWithTierAsync(fallback, audioChunk, true, cancellationToken);
        if (fallbackResult.IsSuccess)
        {
            return fallbackResult;
        }

        return fallbackResult with
        {
            DiagnosticMessage =
                $"Primary failed: {result.DiagnosticMessage}. Fallback failed: {fallbackResult.DiagnosticMessage}"
        };
    }

    public async Task ImportModelFromFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(sourcePath);
        long? totalBytes = fileInfo.Exists ? fileInfo.Length : null;

        // Use a FileStream with FileShare.ReadWrite so we don't conflict with any
        // other handle the OS may still hold after a file-picker dialog.
        await using var src = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 81920,
            useAsync: true);

        await ImportModelFromStreamAsync(src, totalBytes, cancellationToken);
    }

    public async Task ImportModelFromStreamAsync(
        Stream sourceStream,
        long? totalBytes,
        CancellationToken cancellationToken = default)
    {
        await _modelLifecycleLock.WaitAsync(cancellationToken);
        try
        {
            // Hold the inference lock so we cannot swap/dispose the factory while a
            // transcription is in flight — Whisper.net throws "Cannot dispose while
            // processing" if the factory (or its processors) is disposed mid-decode.
            await _inferenceLock.WaitAsync(cancellationToken);
            try
            {
            var tier = _options.PrimaryTier.Trim().ToLowerInvariant();
        if (!_options.TryGetTierDefinition(tier, out var tierDef) || tierDef is null)
            throw new InvalidOperationException($"Unknown model tier '{tier}'.");

        var destPath = ResolvePersistentModelPath(tierDef.ModelPath);
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        // Dispose the factory before writing so the destination file is not locked.
        _activeFactory?.Dispose();
        _activeFactory = null;
        _activeFactoryModelPath = null;
        _isInitialized = false;

        var tempPath = destPath + ".import";

        try
        {
            await using var dst = File.Create(tempPath);

            var buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;

                var fraction = totalBytes > 0 ? (double)received / totalBytes.Value : -1;
                var mb = received / 1_048_576.0;
                var status = totalBytes.HasValue
                    ? $"Importing model… {mb:0.0} MB / {totalBytes.Value / 1_048_576.0:0.0} MB"
                    : $"Importing model… {mb:0.0} MB";

                DownloadProgressChanged?.Invoke(this,
                    new AsrDownloadProgress(tier, fraction, received, totalBytes, status));
            }

            await dst.FlushAsync(cancellationToken);

                var replaced = false;
                for (var attempt = 1; attempt <= 6 && !replaced; attempt++)
                {
                    try
                    {
                        File.Move(tempPath, destPath, overwrite: true);
                        replaced = true;
                    }
                    catch (IOException) when (attempt < 6)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
                    }
                }

                if (!replaced)
                {
                    throw new IOException($"Could not replace model file at '{destPath}' because it is locked.");
                }
            }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }

        DownloadProgressChanged?.Invoke(this,
            new AsrDownloadProgress(tier, 1.0, 0, totalBytes, "✅ Model import complete."));

        _logger.LogInformation(
            "Whisper model for tier '{Tier}' imported to '{Dest}'.", tier, destPath);

            if (!TryBuildFactory(destPath, out var factoryError))
            {
                _logger.LogWarning("Whisper.net factory init after import failed: {Error}", factoryError);
                FallbackToMockOrFail();
                return;
            }

            _isUsingMockMode = false;
            _isInitialized = true;
            ActiveTier = tier;
            _logger.LogInformation("Whisper.net ready from imported model for tier '{Tier}'.", tier);
            }
            finally
            {
                _inferenceLock.Release();
            }
        }
        finally
        {
            _modelLifecycleLock.Release();
        }
    }

    private async Task<RecitationTranscriptionResult> TryTranscribeWithTierAsync(
        string tier,
        byte[] audioChunk,
        bool usedFallback,
        CancellationToken cancellationToken)
    {
        ActiveTier = tier;

        // ✅ No download here — download is exclusively InitializeAsync's responsibility.
        // If the factory is already built for this tier, proceed directly to inference.
        if (_activeFactory is null)
        {
            return RecitationTranscriptionResult.Failure(
                $"Whisper factory not built for tier '{tier}'. Call InitializeAsync first.",
                tier, usedFallback);
        }

        if (!await _inferenceLock.WaitAsync(0, cancellationToken))
        {
            return RecitationTranscriptionResult.Failure("Inference busy; chunk skipped.", tier, usedFallback);
        }

        try
        {
            // Lazy one-shot warmup on the first real transcription so the app launches
            // instantly. Warms the model/interpreter without blocking startup.
            // The inference lock is already held here, so WarmupCore runs in-process
            // (the old path re-entered the lock and deadlocked itself).
            if (!_warmupDone)
            {
                _warmupDone = true;
                await WarmupCoreAsync(cancellationToken);
            }

            return await TranscribeCoreAsync(audioChunk, tier, usedFallback, cancellationToken);
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private async Task<RecitationTranscriptionResult> TranscribeCoreAsync(
        byte[] audioChunk,
        string tier,
        bool usedFallback,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));
        using var linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // _activeFactory is non-null here — TryTranscribeWithTierAsync and
        // ImportModelFromStreamAsync both guard before calling this method.
        var builder = _activeFactory!
            .CreateBuilder()
            .WithLanguage(_options.Language);

        var prompt = BuildPrompt();
        if (!string.IsNullOrWhiteSpace(prompt))
            builder = builder.WithPrompt(prompt);

        builder = builder.WithNoSpeechThreshold(_options.NoSpeechThreshold);

        if (_options.ThreadsOverride > 0)
            builder = builder.WithThreads(_options.ThreadsOverride);

        if (_options.NoTimestamps)
            builder = builder.WithPrintTimestamps(false);

        // Word-level timestamps feed the phoneme-level tajweed analyzer, but DTW
        // alignment roughly doubles-to-triples inference time. The performance
        // profile gates it: Speed = off, Accuracy = on, Auto = on only when the
        // measured real-time factor shows the machine has headroom.
        var useTimestamps = ShouldUseTokenTimestamps(out var timestampsReason);
        if (useTimestamps)
            builder = builder.WithTokenTimestamps();

        // Greedy decoding (BeamSearchWidth <= 1) is dramatically faster than beam
        // search and is sufficient for clean single-speaker Arabic recitation.
        // Beam search is only used for larger tiers where accuracy matters more.
        // The sampling-strategy builder returns to the processor builder via
        // ParentBuilder before Build().
        var processor = _options.BeamSearchWidth > 1
            ? ((BeamSearchSamplingStrategyBuilder)builder
                .WithBeamSearchSamplingStrategy())
                .WithBeamSize(_options.BeamSearchWidth)
                .ParentBuilder
                .Build()
            : builder
                .WithGreedySamplingStrategy()
                .ParentBuilder
                .Build();

        try
        {
            var transcript = new System.Text.StringBuilder();
            var segments = new List<Whisper.net.SegmentData>();
            var segmentCount = 0;
            var totalProbability = 0.0;
            using var audioStream = new MemoryStream(audioChunk);

            _diagnostics.Info(
                $"Whisper inference starting: tier={tier} audioLen={audioChunk.Length} modelPath={_activeFactoryModelPath} lang={_options.Language} timestamps={useTimestamps}");

            try
            {
                await foreach (var segment in processor.ProcessAsync(audioStream, linkedCts.Token))
                {
                    transcript.Append(segment.Text);
                    segments.Add(segment);
                    segmentCount++;
                    totalProbability += segment.Probability;
                }
            }
            catch (Whisper.net.Wave.CorruptedWaveException waveEx)
            {
                _diagnostics.Error($"Whisper.net CORRUPTED WAVE error: {waveEx.Message}", waveEx);
                return RecitationTranscriptionResult.Failure(
                    $"Corrupted wave: {waveEx.Message}", tier, usedFallback);
            }
            catch (Whisper.net.Wave.NotSupportedWaveException waveEx)
            {
                _diagnostics.Error($"Whisper.net UNSUPPORTED WAVE error: {waveEx.Message}", waveEx);
                return RecitationTranscriptionResult.Failure(
                    $"Unsupported wave: {waveEx.Message}", tier, usedFallback);
            }

            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;
            ObserveChunkPerformance(audioChunk, durationMs, timestampsReason);

            _diagnostics.Info(
                $"Whisper inference complete: tier={tier} segments={segmentCount} rawTextLen={transcript.Length} durationMs={durationMs}");

            var text = ArabicNormalizer.Normalize(transcript.ToString().Trim());

            if (string.IsNullOrWhiteSpace(text))
            {
                return RecitationTranscriptionResult.Failure(
                    "Whisper.net produced empty transcript.", tier, usedFallback);
            }

            // Silence is already filtered during decoding via WithNoSpeechThreshold.
            // Do not post-filter on token probabilities: Without WithProbabilities() they
            // are always 0, and even with it enabled Arabic token averages are often low.
            var confidence = segmentCount > 0
                ? (float)(totalProbability / segmentCount)
                : 0.5f;

            // Capture last ~4 words as cross-chunk context for the next chunk.
            // A short prompt keeps decoding fast while still bridging madd (elongation)
            // across chunk boundaries. The surah-name bias is omitted to keep the prompt
            // minimal — the matcher already constrains candidates to the active surah.
            if (!string.IsNullOrWhiteSpace(text))
            {
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var contextWords = words.Length > 4 ? words[^4..] : words;
                _crossChunkContext = string.Join(' ', contextWords);
            }

            var wordTimestamps = BuildWordTimestamps(text, segments);

            return new RecitationTranscriptionResult(true, text, confidence, tier, usedFallback)
            {
                WordTimestamps = wordTimestamps
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RecitationTranscriptionResult.Failure(
                $"Whisper.net inference timed out after {_options.InferenceTimeoutSeconds}s.",
                tier, usedFallback);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper.net inference crashed for tier '{Tier}'.", tier);
            return RecitationTranscriptionResult.Failure(ex.Message, tier, usedFallback);
        }
        finally
        {
            // Whisper.net processors are IAsyncDisposable. DisposeAsync waits for
            // in-flight native work to finish; the synchronous Dispose() throws
            // "Cannot dispose while processing" when a timeout cancelled a decode
            // mid-flight. Dispose here, outside the catch blocks, so the honest
            // timeout message is returned instead of a masked dispose crash.
            await processor.DisposeAsync();
        }
    }

    /// <summary>
    /// Decides whether token timestamps (DTW) are used for this chunk.
    /// Speed: always off. Accuracy: always on. Auto: start off, enable once the
    /// measured real-time factor shows headroom, lock off if a chunk ever gets
    /// too close to the inference timeout.
    /// </summary>
    private bool ShouldUseTokenTimestamps(out string reason)
    {
        var profile = _options.PerformanceProfile?.Trim().ToLowerInvariant();
        if (profile == "speed")
        {
            reason = "Speed";
            return false;
        }

        if (profile == "accuracy")
        {
            reason = "Accuracy";
            return true;
        }

        // Auto (default). The field _timestampsEnabled may be false even on
        // fast machines until enough chunks have been observed — that is the
        // desired "measuring" behavior.
        if (profile == "auto")
        {
            if (_tokenTimestampsLockedOff)
            {
                reason = "Auto: locked off (slow machine)";
                return false;
            }

            reason = _timestampsEnabled ? "Auto: enabled (fast machine)" : "Auto: disabled (measuring)";
            return _timestampsEnabled;
        }

        reason = "Unknown profile (defaulting to Speed)";
        return false;
    }

    /// <summary>
    /// Returns whether token timestamps should currently be used for decoding —
    /// the profile gate used by TranscribeCoreAsync. Exposed for the unit tests
    /// to verify profile behavior without a Whisper model.
    /// </summary>
    internal bool ShouldUseTokenTimestampsForTest()
        => ShouldUseTokenTimestamps(out _);

    /// <summary>
    /// Does this profile allow token timestamps at all? Auto is stateful
    /// (locked-off / measuring / enabled); Speed is always off; Accuracy is
    /// always on. Unknown values are treated as Speed.
    /// </summary>
    internal static bool ProfileAllowsTimestamps(string? profile)
    {
        var normalized = profile?.Trim().ToLowerInvariant();
        return normalized == "auto" || normalized == "accuracy";
    }

    /// <summary>
    /// Updates the Auto-mode RTF (real-time factor = inferenceMs / audioMs) moving
    /// average and flips the DTW gate. Called after every completed chunk.
    /// </summary>
    private void ObserveChunkPerformance(byte[] audioChunk, double inferenceMs, string timestampsReason)
    {
        // Only the stateful Auto profile learns from chunk timing; Speed and
        // Accuracy are pinned, and their observation calls don't mutate state.
        var profileIsAuto = string.Equals(
            _options.PerformanceProfile?.Trim(), "auto", StringComparison.OrdinalIgnoreCase);
        if (!profileIsAuto)
        {
            return;
        }

        var audioMs = audioChunk.Length / 2.0 / 16.0; // 16-bit mono at 16 kHz.
        var rtf = ComputeRtf(audioChunk.Length, inferenceMs);

        _rtfSampleCount++;
        _rtfMovingAverage += (rtf - _rtfMovingAverage) / _rtfSampleCount;

        if (!_timestampsEnabled && _rtfMovingAverage < 0.5 && _rtfSampleCount >= 2)
        {
            _timestampsEnabled = true;
            _logger.LogInformation(
                "Whisper Auto profile: enabling token timestamps (rtf={Rtf:0.00} < 0.5 over {N} chunks).",
                _rtfMovingAverage, _rtfSampleCount);
        }

        // Auto only: never lock Speed/Accuracy — their profile choice is pinned.
        var timeoutBudgetMs = _options.InferenceTimeoutSeconds * 1000.0;
        if (profileIsAuto && inferenceMs > timeoutBudgetMs * 0.6)
        {
            _tokenTimestampsLockedOff = true;
            _timestampsEnabled = false;
            _logger.LogWarning(
                "Whisper Auto profile: chunk took {Ms:0}ms (>60% of {Timeout}s timeout) — locking token timestamps off.",
                inferenceMs, _options.InferenceTimeoutSeconds);
        }

        if (!_profileDecisionLogged)
        {
            _profileDecisionLogged = true;
            _diagnostics.Info(
                $"Whisper profile: {_options.PerformanceProfile} → timestamps={(useTimestampsUnsafe() ? "enabled" : "disabled")} (rtf={_rtfMovingAverage:0.00}, n={_rtfSampleCount})");
        }

        // Local helper so the diagnostics line can reflect the state set above.
        bool useTimestampsUnsafe() => _timestampsEnabled && !_tokenTimestampsLockedOff;
    }

    /// <summary>
    /// Real-time factor: inference wall-time / audio duration (1.0 = decodes at
    /// real-time speed, lower is faster).
    /// </summary>
    private static double ComputeRtf(int audioByteCount, double inferenceMs)
    {
        // 16-bit mono PCM at 16 kHz → 2 bytes per sample, 16000 samples/sec.
        var audioMs = audioByteCount / 2.0 / 16.0;
        return audioMs > 0 ? inferenceMs / audioMs : double.PositiveInfinity;
    }

    // ── Test hooks (used by WhisperProfileTests) ─────────────────────────────

    internal bool GetTimestampsEnabledForTest() => _timestampsEnabled && !_tokenTimestampsLockedOff;
    internal bool GetTimestampsLockedOffForTest() => _tokenTimestampsLockedOff;
    internal string GetTimestampsReasonForTest() => ShouldUseTokenTimestamps(out var reason) ? reason : reason;
    internal void ObserveChunkPerformanceForTest(byte[] audioChunk, double inferenceMs, string timestampsReason)
        => ObserveChunkPerformance(audioChunk, inferenceMs, timestampsReason);
    internal LocalWhisperOptions GetOptionsForTest() => _options;
    internal static double GetRtfForTest(int audioByteCount, double inferenceMs)
        => ComputeRtf(audioByteCount, inferenceMs);
    internal double GetRtfMovingAverageForTest() => _rtfMovingAverage;
    internal int GetRtfSampleCountForTest() => _rtfSampleCount;
    internal string GetProfileForTest() => _options.PerformanceProfile ?? string.Empty;
    internal void ResetProfileStateForTest()
    {
        _tokenTimestampsLockedOff = false;
        _timestampsEnabled = false;
        _profileDecisionLogged = false;
        _rtfSampleCount = 0;
        _rtfMovingAverage = 0.0;
    }

    private string BuildPrompt()
    {
        // The surah name biases Whisper's decoding toward Quranic vocabulary, which is
        // essential for weak tiers (tiny/base) on short surahs like Ikhlas. The matcher
        // still constrains candidates to the active surah downstream; the prompt here
        // just steers the recognizer away from phonetically-similar garbage.
        if (string.IsNullOrWhiteSpace(_crossChunkContext))
        {
            return _surahPrompt ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(_surahPrompt)
            ? _crossChunkContext
            : $"{_surahPrompt} {_crossChunkContext}";
    }

    private static IReadOnlyList<RecitationWordTimestamp> BuildWordTimestamps(
        string text,
        IReadOnlyList<Whisper.net.SegmentData> segments)
    {
        if (string.IsNullOrWhiteSpace(text) || segments.Count == 0)
            return [];

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return [];

        var tokenTimestamps = CollectTokenTimestamps(segments);
        if (tokenTimestamps.Count > 0)
        {
            return BuildWordTimestampsFromTokens(words, tokenTimestamps);
        }

        // Fallback: linear interpolation when token timestamps are unavailable.
        return BuildWordTimestampsFromSegments(text, segments);
    }

    private static IReadOnlyList<(string Text, TimeSpan Start, TimeSpan End)> CollectTokenTimestamps(
        IReadOnlyList<Whisper.net.SegmentData> segments)
    {
        var tokens = new List<(string Text, TimeSpan Start, TimeSpan End)>();
        foreach (var segment in segments)
        {
            if (segment.Tokens is not { Length: > 0 })
                continue;

            foreach (var token in segment.Tokens)
            {
                var tokenText = ArabicNormalizer.Normalize(token.Text?.Trim() ?? string.Empty);
                if (string.IsNullOrWhiteSpace(tokenText) ||
                    tokenText is "." or "," or "。" or "!" or "?" or " " or "\r" or "\n")
                    continue;

                var startTicks = token.DtwTimestamp > 0 ? token.DtwTimestamp : token.Start;
                var endTicks = token.End;
                if (startTicks <= 0 || endTicks <= 0)
                    continue;

                tokens.Add((tokenText, TimeSpan.FromTicks(startTicks), TimeSpan.FromTicks(endTicks)));
            }
        }

        return tokens;
    }

    private static IReadOnlyList<RecitationWordTimestamp> BuildWordTimestampsFromTokens(
        IReadOnlyList<string> words,
        IReadOnlyList<(string Text, TimeSpan Start, TimeSpan End)> tokens)
    {
        var result = new List<RecitationWordTimestamp>(words.Count);
        var tokenIdx = 0;
        for (var wi = 0; wi < words.Count && tokenIdx < tokens.Count; wi++)
        {
            var wordBuilder = new System.Text.StringBuilder();
            var wordStart = tokens[tokenIdx].Start;
            var wordEnd = tokens[tokenIdx].End;

            while (tokenIdx < tokens.Count && wordBuilder.Length < words[wi].Length)
            {
                wordBuilder.Append(tokens[tokenIdx].Text);
                wordEnd = tokens[tokenIdx].End;
                tokenIdx++;
            }

            var mergedText = wordBuilder.ToString();
            if (string.IsNullOrWhiteSpace(mergedText))
                continue;

            var normalizedText = ArabicNormalizer.Normalize(mergedText);
            if (string.IsNullOrWhiteSpace(normalizedText))
                continue;

            result.Add(new RecitationWordTimestamp(
                wi,
                words[wi],
                wordStart,
                wordEnd));
        }

        return result;
    }

    private static IReadOnlyList<RecitationWordTimestamp> BuildWordTimestampsFromSegments(
        string text,
        IReadOnlyList<Whisper.net.SegmentData> segments)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return [];

        var allSegmentText = new System.Text.StringBuilder();
        foreach (var seg in segments)
            allSegmentText.Append(seg.Text);

        var raw = ArabicNormalizer.Normalize(allSegmentText.ToString().Trim());
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var rawWords = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var totalRawChars = rawWords.Sum(w => w.Length);
        if (totalRawChars == 0)
            return [];

        var timestamps = new List<RecitationWordTimestamp>();
        var charPositions = new List<(int WordIdx, int CharStart)>();
        var charOffset = 0;
        for (var wi = 0; wi < rawWords.Length; wi++)
        {
            charPositions.Add((wi, charOffset));
            charOffset += rawWords[wi].Length;
        }

        var totalDuration = segments[^1].End - segments[0].Start;
        var charsPerTick = totalDuration.TotalMilliseconds / Math.Max(totalRawChars, 1);

        foreach (var (wi, charStart) in charPositions)
        {
            var startMs = segments[0].Start.TotalMilliseconds + (charStart * charsPerTick);
            var endMs = startMs + (rawWords[wi].Length * charsPerTick);

            timestamps.Add(new RecitationWordTimestamp(
                wi,
                words.Length > wi ? words[wi] : rawWords[wi],
                TimeSpan.FromMilliseconds(startMs),
                TimeSpan.FromMilliseconds(endMs)));
        }

        return timestamps;
    }

    private async Task WarmupCoreAsync(CancellationToken cancellationToken)
    {
        var silence = BuildSilenceWav(durationMs: 100);
        var warmupResult = await TranscribeCoreAsync(
            silence, _options.PrimaryTier, false, cancellationToken);

        if (warmupResult.IsSuccess)
        {
            _logger.LogInformation("Whisper.net warmup completed for tier '{Tier}'.", warmupResult.TierUsed);
        }
        else
        {
            _logger.LogWarning("Whisper.net warmup failed: {Message}", warmupResult.DiagnosticMessage);
        }
    }

    private bool TryBuildFactory(string modelPath, out string error)
    {
        error = string.Empty;

        if (_activeFactory is not null
            && string.Equals(_activeFactoryModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Guard against missing or corrupt model files BEFORE calling into native code.
        // libggml-whisper.so calls abort() (SIGABRT) on a bad path, which bypasses
        // C# try/catch and kills the process with no recoverable error.
        if (!File.Exists(modelPath))
        {
            error = $"Model file does not exist: '{modelPath}'";
            _logger.LogWarning("TryBuildFactory: {Error}", error);
            return false;
        }

        var fileInfo = new FileInfo(modelPath);
        if (fileInfo.Length < 1024 * 1024) // Sane minimum: 1 MB
        {
            error = $"Model file is too small ({fileInfo.Length} bytes) — likely corrupt or incomplete: '{modelPath}'";
            _logger.LogWarning("TryBuildFactory: {Error} — deleting and will re-download.", error);
            try { File.Delete(modelPath); } catch { /* best effort */ }
            return false;
        }

        try
        {
            _activeFactory?.Dispose();
            _activeFactory = WhisperFactory.FromPath(modelPath);
            _activeFactoryModelPath = modelPath;
            return true;
        }
        catch (Exception ex)
        {
            _activeFactory = null;
            _activeFactoryModelPath = null;
            error = $"Failed to load Whisper model from '{modelPath}': {ex.Message}";
            _logger.LogError(ex, "WhisperFactory.FromPath failed for '{Path}'.", modelPath);
            return false;
        }
    }

    private void FallbackToMockOrFail()
    {
        if (_options.AllowMockWhenUnavailable)
        {
            _isUsingMockMode = true;
            _isInitialized = true;
            _logger.LogInformation("Whisper.net falling back to mock mode (AllowMockWhenUnavailable=true).");
        }
        else
        {
            _isInitialized = false;
        }
    }

    
    private async Task<(bool IsSuccess, string ErrorMessage)> TryExtractModelFromAssetsAsync(
        string tier,
        string targetModelPath,
        WhisperModelTierDefinition tierDef,
        CancellationToken cancellationToken)
    {
        try
        {
            var assetPath = tierDef.ModelPath.Replace("\\", "/");
            _logger.LogInformation("Attempting to extract model from packaged assets: {AssetPath}", assetPath);

            await using var assetStream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            if (assetStream == null)
            {
                return (false, $"Model not found in packaged assets: {assetPath}");
            }

            var dir = Path.GetDirectoryName(targetModelPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = targetModelPath + ".extract";
            await using (var destStream = File.Create(tempPath))
            {
                await assetStream.CopyToAsync(destStream, cancellationToken);
            }

            File.Move(tempPath, targetModelPath, overwrite: true);

            _logger.LogInformation("Model extracted from assets to '{Path}' for tier '{Tier}'", targetModelPath, tier);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract model from assets for tier '{Tier}'", tier);
            return (false, $"Asset extraction failed: {ex.Message}");
        }
    }

    private async Task<(bool IsSuccess, string ErrorMessage)> TryDownloadModelAsync(
        string tier,
        string targetModelPath,
        WhisperModelTierDefinition tierDef,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tierDef.DownloadUrl))
        {
            return (false,
                $"Model file not found at '{targetModelPath}', and no download URL configured for tier '{tier}'.");
        }

        // ✅ Prevent concurrent downloads of the same model (e.g. rapid restarts).
        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check inside the lock — another caller may have finished.
            if (File.Exists(targetModelPath))
            {
                _logger.LogInformation(
                    "Model for tier '{Tier}' already present after lock — skipping download.", tier);
                return (true, string.Empty);
            }

            var dir = Path.GetDirectoryName(targetModelPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _logger.LogInformation("Downloading Whisper model for tier '{Tier}' from {Url}.",
                tier, tierDef.DownloadUrl);

            using var response = await SharedHttpClient.GetAsync(
                tierDef.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (false,
                    $"Failed to download model for tier '{tier}'. HTTP {(int)response.StatusCode}.");
            }

            var totalBytes = response.Content.Headers.ContentLength;
            var tempPath = targetModelPath + ".download";

            try
            {
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var dest = File.Create(tempPath);

                var buffer = new byte[81920];
                long received = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;

                    var fraction = totalBytes > 0 ? (double)received / totalBytes.Value : -1;
                    var mb = received / 1_048_576.0;
                    var status = totalBytes.HasValue
                        ? $"Downloading {tier} model… {mb:0.0} MB / {totalBytes.Value / 1_048_576.0:0.0} MB"
                        : $"Downloading {tier} model… {mb:0.0} MB received";

                    DownloadProgressChanged?.Invoke(this,
                        new AsrDownloadProgress(tier, fraction, received, totalBytes, status));
                }

                await dest.FlushAsync(cancellationToken);
                File.Move(tempPath, targetModelPath, overwrite: true);
            }
            catch
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                throw;
            }

            DownloadProgressChanged?.Invoke(this,
                new AsrDownloadProgress(tier, 1.0, 0, totalBytes, $"✅ {tier} model download complete."));

            _logger.LogInformation("Whisper model for tier '{Tier}' saved to '{Path}'.", tier, targetModelPath);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to download model for tier '{tier}': {ex.Message}");
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Resolves a model filename to a persistent, writable user-data directory.
    /// Uses FileSystem.AppDataDirectory so the file survives app restarts on all platforms.
    /// </summary>
    private static string ResolvePersistentModelPath(string configuredPath)
    {
        // If already an absolute path, trust it.
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        // ✅ AppDataDirectory is writable and persistent on Windows, Android, iOS, macOS.
        var dataDir = FileSystem.Current.AppDataDirectory;
        return Path.GetFullPath(Path.Combine(dataDir, configuredPath));
    }

    private (string Tier, string Path)? ResolveUsableFallbackTier(string primaryTier)
    {
        // Prefer the explicitly configured fallback tier if its model is on disk.
        var fallbackName = _options.FallbackTier?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(fallbackName)
            && !string.Equals(fallbackName, primaryTier, StringComparison.Ordinal)
            && _options.TryGetTierDefinition(fallbackName, out var fbDef) && fbDef is not null)
        {
            var fbPath = ResolvePersistentModelPath(fbDef.ModelPath);
            if (File.Exists(fbPath))
            {
                return (fallbackName, fbPath);
            }
        }

        // Otherwise use any tier whose model file is already present on disk.
        foreach (var candidate in new[] { "base", "small", "medium", "tiny" })
        {
            if (string.Equals(candidate, primaryTier, StringComparison.Ordinal))
            {
                continue;
            }

            if (_options.TryGetTierDefinition(candidate, out var def) && def is not null)
            {
                var path = ResolvePersistentModelPath(def.ModelPath);
                if (File.Exists(path))
                {
                    return (candidate, path);
                }
            }
        }

        return null;
    }

    private static byte[] BuildSilenceWav(int durationMs)
    {
        const int sampleRate = 16000;
        const short bitsPerSample = 16;
        const short channels = 1;
        int sampleCount = sampleRate * durationMs / 1000;
        int dataSize = sampleCount * (bitsPerSample / 8);

        using var ms = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);

        return ms.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        // Wait for any in-flight transcription to finish before disposing the factory,
        // otherwise Whisper.net throws "Cannot dispose while processing".
        try
        {
            await _inferenceLock.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (OperationCanceledException)
        {
            // Timed out waiting; proceed with disposal best-effort.
        }

        _activeFactory?.Dispose();
        _activeFactory = null;
        _inferenceLock.Dispose();
        _downloadLock.Dispose();
        _modelLifecycleLock.Dispose();
        await ValueTask.CompletedTask;
    }
}
