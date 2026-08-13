using TarteelMobile.Services;
using TarteelMobile.Services.Asr;
using Xunit;

namespace TarteelMobile.Tests;

/// <summary>
/// Tests for the adaptive Whisper performance profile (Auto mode):
/// token timestamps (DTW) start off, enable when the measured real-time
/// factor shows headroom, and lock off if a chunk approaches the timeout.
/// The engine state machine is exercised directly without a Whisper model.
/// </summary>
public sealed class WhisperProfileTests
{
    // 16-bit mono PCM at 16 kHz → 2 bytes per sample, 16000 samples/sec.
    private static byte[] AudioOfSeconds(double seconds) => new byte[(int)(seconds * 32000)];

    // ── Profile pinning ─────────────────────────────────────────────────────

    [Fact]
    public void SpeedProfile_NeverEnablesTimestamps()
    {
        var engine = new ProfileTestHarness(profile: "Speed");

        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 1000);
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 900);

        Assert.False(engine.IsTimestampsEnabled);
    }

    [Fact]
    public void AccuracyProfile_AlwaysEnablesTimestamps()
    {
        var engine = new ProfileTestHarness(profile: "Accuracy");

        // Accuracy reports timestamps on even before any chunk is observed,
        // because it does not depend on measured performance.
        Assert.True(engine.IsTimestampsEnabled);

        // And a slow chunk does not switch it off.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 40000);
        Assert.True(engine.IsTimestampsEnabled);
    }

    [Fact]
    public void AccuracyProfile_SlowChunkDoesNotLock()
    {
        var engine = new ProfileTestHarness(profile: "Accuracy");

        // A slow chunk under Accuracy never arms the Auto lock.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 40000);
        Assert.False(engine.IsLockedOff);
        Assert.True(engine.IsTimestampsEnabled);
    }

    [Fact]
    public void AutoProfile_SlowMachine_StaysDisabled()
    {
        var engine = new ProfileTestHarness(profile: "Auto");

        // 9s chunks taking ~30s → rtf ≈ 3.3 — way too slow, must never enable.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 30000);
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 28000);

        Assert.False(engine.IsTimestampsEnabled);
        Assert.True(engine.IsLockedOff); // >60% of the 45s budget → locked off
    }

    [Fact]
    public void AutoProfile_FastMachine_EnablesAfterTwoChunks()
    {
        var engine = new ProfileTestHarness(profile: "Auto");

        // 9s audio decoded in 3s → rtf ≈ 0.33 < 0.5. Enables after 2 samples.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 3000);
        Assert.False(engine.IsTimestampsEnabled);

        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 3200);
        Assert.True(engine.IsTimestampsEnabled);
    }

    // ── Auto mode ───────────────────────────────────────────────────────────
    [Fact]
    public void AutoProfile_ChunkNearTimeout_LocksOffAfterEnable()
    {
        var engine = new ProfileTestHarness(profile: "Auto");

        // Machine was fast at startup (enables DTW)…
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 3000);
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 3200);
        Assert.True(engine.IsTimestampsEnabled);

        // …then throttles and a chunk takes 40s (>60% of 45s) → locked off.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 40000);
        Assert.False(engine.IsTimestampsEnabled);
        Assert.True(engine.IsLockedOff);
    }

    [Fact]
    public void Profiles_UseConfiguredTimeoutBudget()
    {
        var engine = new ProfileTestHarness(profile: "Auto", timeoutSeconds: 20);

        // 13s on a 20s budget = 65% → locked off.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 13000);

        Assert.True(engine.IsLockedOff);
        Assert.Equal(20, engine.GetTimeoutSecondsForTest());
    }

    [Fact]
    public void Rtf_ComputesInferenceTimeOverAudioTime()
    {
        // 9s of 16-bit mono audio = 288000 bytes.
        var rtf = LocalWhisperAsrEngine.GetRtfForTest(audioByteCount: 288000, inferenceMs: 9000);
        Assert.Equal(1.0, rtf, precision: 3);

        // 3s inference on 9s audio → rtf ≈ 0.33.
        var fastRtf = LocalWhisperAsrEngine.GetRtfForTest(audioByteCount: 288000, inferenceMs: 3000);
        Assert.Equal(0.333, fastRtf, precision: 3);
    }

    [Fact]
    public void AutoProfile_EnableUsesMovingAverageNotSingleChunk()
    {
        var engine = new ProfileTestHarness(profile: "Auto");

        // First chunk very fast (rtf 0.1) — below the 2-sample minimum, so no enable yet.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 900);
        Assert.False(engine.IsTimestampsEnabled);
        Assert.Equal(1, engine.RtfSampleCount);

        // Second chunk slow (rtf 3.0) — average ≈ 1.55 → must not enable.
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 27000);
        Assert.False(engine.IsTimestampsEnabled);
    }

    [Fact]
    public void AutoProfile_MovingAverageReflectsObservedChunks()
    {
        var engine = new ProfileTestHarness(profile: "Auto");

        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 9000);  // rtf 1.0
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 9000);  // rtf 1.0
        engine.ObserveChunk(audioSeconds: 9, inferenceMs: 9000);  // rtf 1.0

        Assert.Equal(3, engine.RtfSampleCount);
        Assert.Equal(1.0, engine.RtfMovingAverage, precision: 3);
    }

    /// <summary>
    /// Drives the engine's private profile state through public methods so the
    /// state machine is tested exactly as the real engine uses it.
    /// </summary>
    private sealed class ProfileTestHarness
    {
        private readonly LocalWhisperAsrEngine _engine;

        public ProfileTestHarness(string profile, int timeoutSeconds = 45)
        {
            var options = new LocalWhisperOptions
            {
                PerformanceProfile = profile,
                InferenceTimeoutSeconds = timeoutSeconds
            };
            _engine = new LocalWhisperAsrEngine(
                Microsoft.Extensions.Options.Options.Create(options),
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalWhisperAsrEngine>(),
                new NullDiagnostics());
        }

        public bool IsTimestampsEnabled => _engine.ShouldUseTokenTimestampsForTest();
        public bool IsLockedOff => _engine.GetTimestampsLockedOffForTest();
        public int RtfSampleCount => _engine.GetRtfSampleCountForTest();
        public double RtfMovingAverage => _engine.GetRtfMovingAverageForTest();

        public void ObserveChunk(double audioSeconds, double inferenceMs)
        {
            var audio = AudioOfSeconds(audioSeconds);
            var reason = _engine.GetTimestampsReasonForTest();
            _engine.ObserveChunkPerformanceForTest(audio, inferenceMs, reason);
        }

        public void SetProfile(string profile) => _engine.GetOptionsForTest().PerformanceProfile = profile;

        public int GetTimeoutSecondsForTest() => _engine.GetOptionsForTest().InferenceTimeoutSeconds;
    }

    private sealed class NullDiagnostics : IAppDiagnosticsService
    {
        public string LogPath => string.Empty;
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception? exception = null) { }
        public Task<IReadOnlyList<string>> ReadRecentAsync(int maxLines = 200) => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
