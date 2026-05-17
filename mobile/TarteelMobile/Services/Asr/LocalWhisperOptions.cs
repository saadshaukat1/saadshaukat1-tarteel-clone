namespace TarteelMobile.Services.Asr;

public sealed class LocalWhisperOptions
{
    public const string SectionName = "LocalWhisper";

    public bool Enabled { get; set; } = true;
    public bool PreferLocalEngine { get; set; } = true;
    public string PrimaryTier { get; set; } = "small";
    public string? FallbackTier { get; set; } = "small";
    public bool WarmupOnStartup { get; set; } = true;
    public bool AllowMockWhenUnavailable { get; set; } = true;
    public bool AutoDiscoverPaths { get; set; } = true;
    public string RuntimePath { get; set; } = string.Empty;
    public string RuntimeArgumentsTemplate { get; set; } =
        "-m \"{modelPath}\" -f \"{audioPath}\" -l {language} --no-timestamps";
    public string Language { get; set; } = "ar";
    public int InferenceTimeoutSeconds { get; set; } = 20;

    public WhisperModelTierDefinition Small { get; set; } = new();
    public WhisperModelTierDefinition Medium { get; set; } = new();

    public bool TryGetTierDefinition(string tierName, out WhisperModelTierDefinition? definition)
    {
        definition = tierName.Trim().ToLowerInvariant() switch
        {
            "small" => Small,
            "medium" => Medium,
            _ => null
        };

        return definition is not null;
    }
}

public sealed class WhisperModelTierDefinition
{
    public string ModelPath { get; set; } = string.Empty;
}
