namespace TarteelMobile.Services;

/// <summary>
/// Local SQLite and bootstrap data options for offline Quran access.
/// These defaults work out of the box with a packaged seed dataset.
/// </summary>
public sealed class LocalQuranDataOptions
{
    public const string SectionName = "LocalQuranData";

    public string DatabaseFileName { get; set; } = "quran-local.db";
    public string DefaultTranslationLanguage { get; set; } = "en";
    public string DefaultTranslator { get; set; } = "Saheeh International";
    public string DefaultUserKey { get; set; } = "offline-default";

    // Preferred path for a future full Quran import payload in app assets.
    public string PreferredAssetImportFile { get; set; } = "quran/import/full_quran.json";

    // Practical fallback shipped now so the app can run fully offline.
    public string FallbackSeedAssetImportFile { get; set; } = "quran/import/seed_verses.json";

    // Optional absolute/relative external file path for local desktop imports.
    public string? ExternalImportFile { get; set; }
}
