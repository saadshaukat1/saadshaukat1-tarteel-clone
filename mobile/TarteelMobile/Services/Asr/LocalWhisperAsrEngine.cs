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
public sealed class LocalWhisperAsrEngine(
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

        // ✅ Resolve to persistent writable storage — survives app restarts.
        var modelPath = ResolvePersistentModelPath(tierDef.ModelPath);

                    if (!File.Exists(modelPath))
            {
                _logger.LogInformation(
                    "Model not found at '{Path}' for tier '{Tier}' — trying to extract from packaged assets.", modelPath, tier);

                // Try to extract from packaged MauiAssets first
                var extract = await TryExtractModelFromAssetsAsync(tier, modelPath, tierDef, cancellationToken);
                if (!extract.IsSuccess)
                {
                    _logger.LogInformation(
                        "Model extraction from assets failed for tier '{Tier}': {Error} — falling back to download.",
                        tier, extract.ErrorMessage);

                    var download = await TryDownloadModelAsync(tier, modelPath, tierDef, cancellationToken);
                    if (!download.IsSuccess)
                    {
                        _logger.LogWarning("Model download failed for tier '{Tier}': {Error}", tier, download.ErrorMessage);
                        FallbackToMockOrFail();
                        return;
                    }
                }
            }
        else
        {
            _logger.LogInformation(
                "Model already present at '{Path}' for tier '{Tier}' — skipping download.", modelPath, tier);
        }

        if (!TryBuildFactory(modelPath, out var factoryError))
        {
            _logger.LogWarning("Whisper.net factory init failed: {Error}", factoryError);
            FallbackToMockOrFail();
            return;
        }

        _isInitialized = true;
        _logger.LogInformation("Whisper.net initialized with tier '{Tier}' from '{Path}'.", ActiveTier, modelPath);
        _diagnostics.Info(
            $"Whisper config: language={_options.Language} noSpeechThresh={_options.NoSpeechThreshold} entropyThresh={_options.EntropyThreshold} beamSize={_options.BeamSearchWidth} temp={_options.Temperature} threads={_options.ThreadsOverride} noTimestamps={_options.NoTimestamps}");

        if (_options.WarmupOnStartup && !_warmupDone)
        {
            _warmupDone = true;
            await WarmupAsync(cancellationToken);
        }
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

        var primary = _options.PrimaryTier.Trim().ToLowerInvariant();
        var fallback = _options.FallbackTier?.Trim().ToLowerInvariant();

        var result = await TryTranscribeWithTierAsync(primary, audioChunk, false, cancellationToken);
        if (result.IsSuccess)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(fallback)
            && !string.Equals(primary, fallback, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Primary tier '{Primary}' failed. Trying fallback '{Fallback}'. Reason: {Reason}",
                primary, fallback, result.DiagnosticMessage);

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

        return result;
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
            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));
            using var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var builder = _activeFactory
                .CreateBuilder()
                .WithLanguage(_options.Language);

            var prompt = BuildPrompt();
            if (!string.IsNullOrWhiteSpace(prompt))
                builder = builder.WithPrompt(prompt);

            builder = builder.WithNoSpeechThreshold(1.0f);

            if (_options.ThreadsOverride > 0)
                builder = builder.WithThreads(_options.ThreadsOverride);

            if (_options.NoTimestamps)
                builder = builder.WithPrintTimestamps(false);

            using var processor = builder.Build();

            var transcript = new System.Text.StringBuilder();
            var segmentCount = 0;
            var totalProbability = 0.0;
            using var audioStream = new MemoryStream(audioChunk);

            _diagnostics.Info(
                $"Whisper inference starting: tier={tier} audioLen={audioChunk.Length} modelPath={_activeFactoryModelPath} lang={_options.Language}");

            try
            {
                await foreach (var segment in processor.ProcessAsync(audioStream, linkedCts.Token))
                {
                    transcript.Append(segment.Text);
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

            _diagnostics.Info(
                $"Whisper inference complete: tier={tier} segments={segmentCount} rawTextLen={transcript.Length}");

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

            // Capture last ~8 words as cross-chunk context for the next chunk.
            // Quranic recitation has extended madd (elongation) — 8 words provide
            // stronger continuity across chunk boundaries.
            if (!string.IsNullOrWhiteSpace(text))
            {
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var contextWords = words.Length > 8 ? words[^8..] : words;
                _crossChunkContext = string.Join(' ', contextWords);
            }

            return new RecitationTranscriptionResult(true, text, confidence, tier, usedFallback);
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
            _inferenceLock.Release();
        }
    }

    private string BuildPrompt()
    {
        if (!string.IsNullOrWhiteSpace(_crossChunkContext) && !string.IsNullOrWhiteSpace(_surahPrompt))
            return $"{_crossChunkContext} {_surahPrompt}";
        if (!string.IsNullOrWhiteSpace(_crossChunkContext))
            return _crossChunkContext;
        return _surahPrompt ?? string.Empty;
    }

    private async Task WarmupAsync(CancellationToken cancellationToken)
    {
        var silence = BuildSilenceWav(durationMs: 100);
        var warmupResult = await TryTranscribeWithTierAsync(
            _options.PrimaryTier, silence, false, cancellationToken);

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
        _activeFactory?.Dispose();
        _activeFactory = null;
        _inferenceLock.Dispose();
        _downloadLock.Dispose();
        _modelLifecycleLock.Dispose();
        await ValueTask.CompletedTask;
    }
}
