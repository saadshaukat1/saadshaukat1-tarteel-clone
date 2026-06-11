namespace TarteelMobile.Services.Asr;

public sealed class LocalWhisperOptions
{
    public const string SectionName = "LocalWhisper";

    public bool Enabled { get; set; } = true;
    public bool PreferLocalEngine { get; set; } = true;
    public string PrimaryTier { get; set; } = "base";
    public string? FallbackTier { get; set; } = "base";
    public bool WarmupOnStartup { get; set; } = true;
    public bool AllowMockWhenUnavailable { get; set; } = true;
    public string Language { get; set; } = "ar";
    public int InferenceTimeoutSeconds { get; set; } = 30;

    // Whisper inference tuning
    public int BeamSearchWidth { get; set; } = 12;
    public float Temperature { get; set; } = 0.0f;
    public int ThreadsOverride { get; set; } = 0;
    public float NoSpeechThreshold { get; set; } = 0.6f;
    public float EntropyThreshold { get; set; } = 2.4f;
    public bool NoTimestamps { get; set; } = true;

    public WhisperModelTierDefinition Base   { get; set; } = new();
    public WhisperModelTierDefinition Small  { get; set; } = new();
    public WhisperModelTierDefinition Medium { get; set; } = new();

    public bool TryGetTierDefinition(string tierName, out WhisperModelTierDefinition? definition)
    {
        definition = tierName.Trim().ToLowerInvariant() switch
        {
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
