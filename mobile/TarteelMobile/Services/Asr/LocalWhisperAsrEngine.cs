using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TarteelClone.LocalRecitationCore.Models;

namespace TarteelMobile.Services.Asr;

/// <summary>
/// Practical local Whisper adapter for desktop/offline runs.
/// It stays compile-safe when native runtime/model assets are not yet installed.
/// </summary>
public sealed class LocalWhisperAsrEngine(
    IOptions<LocalWhisperOptions> options,
    ILogger<LocalWhisperAsrEngine> logger) : IAsrEngine
{
    private const string DefaultRuntimeFileName = "whisper-cli.exe";
    private static readonly HttpClient SharedHttpClient = new();
    private static readonly string[] SupportedModelExtensions = [".bin", ".gguf"];

    private readonly LocalWhisperOptions _options = options.Value;
    private readonly ILogger<LocalWhisperAsrEngine> _logger = logger;
    private bool _isInitialized;
    private bool _isUsingMockMode;
    private string? _resolvedRuntimePath;
    private readonly Dictionary<string, string> _resolvedTierModelPaths = new(StringComparer.OrdinalIgnoreCase);

    public bool IsReady => _isInitialized;
    public string ActiveTier { get; private set; } = string.Empty;
    public bool IsUsingMockMode => _isUsingMockMode;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var configuredTier = _options.PrimaryTier.Trim().ToLowerInvariant();
        ActiveTier = configuredTier;
        _isUsingMockMode = false;
        _resolvedTierModelPaths.Clear();
        _resolvedRuntimePath = ResolveRuntimePath();

        if (!_options.Enabled || !_options.PreferLocalEngine)
        {
            _logger.LogInformation(
                "Local Whisper disabled by config. Enabled={Enabled} PreferLocalEngine={PreferLocalEngine}",
                _options.Enabled,
                _options.PreferLocalEngine);
            _isInitialized = false;
            return;
        }

        if (!TryValidateRuntime(configuredTier, out var setupError))
        {
            _logger.LogWarning("Local Whisper init blocked: {SetupError}", setupError);
            _isInitialized = false;
            _isUsingMockMode = false;
            return;
        }

        _isInitialized = true;
        _logger.LogInformation("Local Whisper initialized with tier {Tier}", ActiveTier);

        if (_options.WarmupOnStartup)
        {
            await WarmupAsync(cancellationToken);
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

        var primary = _options.PrimaryTier.Trim().ToLowerInvariant();
        var fallback = _options.FallbackTier?.Trim().ToLowerInvariant();

        var primaryResult = await TryTranscribeWithTierAsync(primary, audioChunk, false, cancellationToken);
        if (primaryResult.IsSuccess)
        {
            return primaryResult;
        }

        if (!string.IsNullOrWhiteSpace(fallback) && !string.Equals(primary, fallback, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Primary tier {PrimaryTier} failed. Trying fallback tier {FallbackTier}. Reason: {Reason}",
                primary,
                fallback,
                primaryResult.DiagnosticMessage);

            var fallbackResult = await TryTranscribeWithTierAsync(fallback, audioChunk, true, cancellationToken);
            if (fallbackResult.IsSuccess)
            {
                return fallbackResult;
            }

            return fallbackResult with
            {
                DiagnosticMessage = $"Primary failed: {primaryResult.DiagnosticMessage}. Fallback failed: {fallbackResult.DiagnosticMessage}"
            };
        }

        return primaryResult;
    }

    private async Task WarmupAsync(CancellationToken cancellationToken)
    {
        // Small silence buffer for startup path validation and model preload.
        var silenceChunk = BuildWaveFileBytes(new byte[3200]);
        var warmupResult = await TryTranscribeWithTierAsync(_options.PrimaryTier, silenceChunk, false, cancellationToken);

        if (warmupResult.IsSuccess)
        {
            _logger.LogInformation("Local Whisper warmup completed for tier {Tier}", warmupResult.TierUsed);
            return;
        }

        _logger.LogWarning("Local Whisper warmup failed: {Message}", warmupResult.DiagnosticMessage);
    }

    private async Task<RecitationTranscriptionResult> TryTranscribeWithTierAsync(
        string tier,
        byte[] audioChunk,
        bool usedFallback,
        CancellationToken cancellationToken)
    {
        ActiveTier = tier;

        if (!_options.TryGetTierDefinition(tier, out var tierDefinition) || tierDefinition is null)
        {
            return RecitationTranscriptionResult.Failure($"Unknown model tier '{tier}'.", tier, usedFallback);
        }

        var resolvedModelPath = ResolveModelPath(tier, tierDefinition);
        if (string.IsNullOrWhiteSpace(resolvedModelPath))
        {
            return RecitationTranscriptionResult.Failure($"Model path is empty for tier '{tier}'.", tier, usedFallback);
        }

        if (!File.Exists(resolvedModelPath))
        {
            var downloadResult = await TryDownloadModelAsync(tier, resolvedModelPath, tierDefinition, cancellationToken);
            if (!downloadResult.IsSuccess)
            {
                return RecitationTranscriptionResult.Failure(downloadResult.ErrorMessage, tier, usedFallback);
            }
        }

        if (!TryValidateRuntime(tier, out var validationError))
        {
            return RecitationTranscriptionResult.Failure(validationError, tier, usedFallback);
        }

        var tempAudioPath = Path.Combine(
            Path.GetTempPath(),
            $"tarteel-asr-{Guid.NewGuid():N}.wav");
        Process? process = null;

        try
        {
            await File.WriteAllBytesAsync(tempAudioPath, audioChunk, cancellationToken);

            _isUsingMockMode = false;
            process = CreateWhisperProcess(resolvedModelPath, tempAudioPath);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.InferenceTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var error = $"Whisper process exited with code {process.ExitCode}. {stderr}".Trim();
                _logger.LogWarning("Local Whisper process failed: {Error}", error);
                return RecitationTranscriptionResult.Failure(error, tier, usedFallback);
            }

            var transcript = ExtractTranscript(stdout);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return RecitationTranscriptionResult.Failure("Whisper produced empty transcript.", tier, usedFallback);
            }

            return new RecitationTranscriptionResult(
                true,
                transcript,
                0.75f,
                tier,
                usedFallback);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return RecitationTranscriptionResult.Failure(
                $"Whisper inference timed out after {_options.InferenceTimeoutSeconds}s.",
                tier,
                usedFallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local Whisper inference crashed for tier {Tier}", tier);
            return RecitationTranscriptionResult.Failure(ex.Message, tier, usedFallback);
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort kill only.
                }
            }

            process?.Dispose();
            TryDeleteTempFile(tempAudioPath);
        }
    }

    private async Task<(bool IsSuccess, string ErrorMessage)> TryDownloadModelAsync(
        string tier,
        string targetModelPath,
        WhisperModelTierDefinition tierDefinition,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tierDefinition.DownloadUrl))
        {
            return (false, $"Whisper model file not found at '{targetModelPath}', and no download URL is configured for tier '{tier}'.");
        }

        try
        {
            var modelDirectory = Path.GetDirectoryName(targetModelPath);
            if (!string.IsNullOrWhiteSpace(modelDirectory))
            {
                Directory.CreateDirectory(modelDirectory);
            }

            _logger.LogInformation("Downloading Whisper model for tier {Tier} from {Url}", tier, tierDefinition.DownloadUrl);

            using var response = await SharedHttpClient.GetAsync(
                tierDefinition.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Failed to download model for tier '{tier}'. HTTP {(int)response.StatusCode}.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(targetModelPath);
            await source.CopyToAsync(destination, cancellationToken);

            if (!File.Exists(targetModelPath))
            {
                return (false, $"Downloaded model for tier '{tier}' did not persist to '{targetModelPath}'.");
            }

            _logger.LogInformation("Whisper model for tier {Tier} downloaded to {Path}", tier, targetModelPath);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to download model for tier '{tier}': {ex.Message}");
        }
    }

    private Process CreateWhisperProcess(string modelPath, string audioPath)
    {
        var args = _options.RuntimeArgumentsTemplate
            .Replace("{modelPath}", modelPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{audioPath}", audioPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{language}", _options.Language, StringComparison.OrdinalIgnoreCase);

        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _resolvedRuntimePath ?? _options.RuntimePath,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
    }

    private bool TryValidateRuntime(string tier, out string error)
    {
        error = string.Empty;

        var runtimePath = ResolveRuntimePath();
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            error = "LocalWhisper:RuntimePath is empty.";
            return false;
        }

        if (!_options.TryGetTierDefinition(tier, out var tierDefinition) || tierDefinition is null)
        {
            error = $"LocalWhisper tier '{tier}' is not configured.";
            return false;
        }

        var modelPath = ResolveModelPath(tier, tierDefinition);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            error = $"Model path is empty for tier '{tier}'.";
            return false;
        }

        if (!File.Exists(runtimePath))
        {
            error = $"Whisper runtime not found at '{runtimePath}'.";
            return false;
        }

        if (!File.Exists(modelPath))
        {
            error = $"Whisper model file not found at '{modelPath}'.";
            return false;
        }

        return true;
    }

    private string ResolveRuntimePath()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedRuntimePath))
        {
            return _resolvedRuntimePath;
        }

        if (!string.IsNullOrWhiteSpace(_options.RuntimePath))
        {
            _resolvedRuntimePath = ExpandPath(_options.RuntimePath);
            return _resolvedRuntimePath;
        }

        if (!_options.AutoDiscoverPaths)
        {
            return string.Empty;
        }

        foreach (var root in GetSearchRoots())
        {
            var candidates = new[]
            {
                Path.Combine(root, "offline", "extras", DefaultRuntimeFileName),
                Path.Combine(root, "offline", "models", DefaultRuntimeFileName),
                Path.Combine(root, "offline-assets", "extras", DefaultRuntimeFileName),
                Path.Combine(root, "offline-assets", "models", DefaultRuntimeFileName),
                Path.Combine(root, DefaultRuntimeFileName)
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    _resolvedRuntimePath = candidate;
                    _logger.LogInformation("Auto-discovered Whisper runtime at '{RuntimePath}'.", candidate);
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    private string ResolveModelPath(string tier, WhisperModelTierDefinition tierDefinition)
    {
        if (_resolvedTierModelPaths.TryGetValue(tier, out var cachedPath))
        {
            return cachedPath;
        }

        if (!string.IsNullOrWhiteSpace(tierDefinition.ModelPath))
        {
            var configuredPath = ExpandPath(tierDefinition.ModelPath);
            _resolvedTierModelPaths[tier] = configuredPath;
            return configuredPath;
        }

        if (!_options.AutoDiscoverPaths)
        {
            return string.Empty;
        }

        var discovered = TryAutoDiscoverTierModelPath(tier);
        if (!string.IsNullOrWhiteSpace(discovered))
        {
            _resolvedTierModelPaths[tier] = discovered;
            _logger.LogInformation("Auto-discovered Whisper model for tier '{Tier}' at '{ModelPath}'.", tier, discovered);
            return discovered;
        }

        return string.Empty;
    }

    private string TryAutoDiscoverTierModelPath(string tier)
    {
        foreach (var root in GetSearchRoots())
        {
            var modelRoots = new[]
            {
                Path.Combine(root, "offline", "models"),
                Path.Combine(root, "offline-assets", "models"),
                root
            };

            foreach (var modelRoot in modelRoots)
            {
                if (!Directory.Exists(modelRoot))
                {
                    continue;
                }

                var exactTierFile = Directory
                    .EnumerateFiles(modelRoot, $"*{tier}*.*", SearchOption.AllDirectories)
                    .FirstOrDefault(IsSupportedModelFile);
                if (!string.IsNullOrWhiteSpace(exactTierFile))
                {
                    return exactTierFile;
                }
            }
        }

        foreach (var root in GetSearchRoots())
        {
            var modelRoots = new[]
            {
                Path.Combine(root, "offline", "models"),
                Path.Combine(root, "offline-assets", "models"),
                root
            };

            foreach (var modelRoot in modelRoots)
            {
                if (!Directory.Exists(modelRoot))
                {
                    continue;
                }

                var anyModel = Directory
                    .EnumerateFiles(modelRoot, "*.*", SearchOption.AllDirectories)
                    .FirstOrDefault(IsSupportedModelFile);
                if (!string.IsNullOrWhiteSpace(anyModel))
                {
                    return anyModel;
                }
            }
        }

        return string.Empty;
    }

    private static bool IsSupportedModelFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedModelExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var roots = new List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        for (var depth = 0; depth < 10 && current is not null; depth++)
        {
            roots.Add(current.FullName);
            current = current.Parent;
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractTranscript(string stdout)
    {
        var lines = stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bestLine = lines.LastOrDefault(line => !line.StartsWith("[", StringComparison.Ordinal))
            ?? lines.LastOrDefault()
            ?? string.Empty;

        return bestLine.Trim();
    }

    private static byte[] BuildWaveFileBytes(
        byte[] pcmBytes,
        int sampleRateHz = 16000,
        short bitsPerSample = 16,
        short channels = 1)
    {
        var blockAlign = (short)(channels * (bitsPerSample / 8));
        var byteRate = sampleRateHz * blockAlign;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRateHz);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);
        writer.Flush();

        return stream.ToArray();
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
