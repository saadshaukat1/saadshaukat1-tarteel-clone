namespace TarteelMobile.Services.Asr;

public sealed class LocalWhisperOptions
{
    public const string SectionName = "LocalWhisper";

    public bool Enabled { get; set; } = true;
    public bool PreferLocalEngine { get; set; } = true;
    public string PrimaryTier { get; set; } = "base";
    public string? FallbackTier { get; set; } = "small";
    public bool WarmupOnStartup { get; set; } = false;
    public bool AllowMockWhenUnavailable { get; set; } = true;
    public string Language { get; set; } = "ar";
    public int InferenceTimeoutSeconds { get; set; } = 45;

    /// <summary>
    /// Performance trade-off profile for the ASR engine:
    /// 'Speed'    — token timestamps (DTW alignment) always off; fastest streaming.
    /// 'Accuracy' — token timestamps always on; slower inference, precise phoneme timing.
    /// 'Auto'     — start with timestamps off, enable once measured real-time factor
    ///              (inferenceMs / audioMs) shows headroom, and lock off again if a
    ///              chunk gets too close to the inference timeout.
    /// </summary>
    public string PerformanceProfile { get; set; } = "Auto";

    // Whisper inference tuning
    public int BeamSearchWidth { get; set; } = 1;
    public float Temperature { get; set; } = 0.0f;
    public int ThreadsOverride { get; set; } = 0;
    public float NoSpeechThreshold { get; set; } = 0.6f;
    public float EntropyThreshold { get; set; } = 2.4f;
    public bool NoTimestamps { get; set; } = true;

    public WhisperModelTierDefinition Tiny   { get; set; } = new();
    public WhisperModelTierDefinition Base   { get; set; } = new();
    public WhisperModelTierDefinition Small  { get; set; } = new();
    public WhisperModelTierDefinition Medium { get; set; } = new();

    public bool TryGetTierDefinition(string tierName, out WhisperModelTierDefinition? definition)
    {
        definition = tierName.Trim().ToLowerInvariant() switch
        {
            "tiny"   => Tiny,
            "base"   => Base,
            "small"  => Small,
            "medium" => Medium,
            _ => null
        };

        return definition is not null;
    }
}

public sealed class WhisperModelTierDefinition
{
    public string ModelPath { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
