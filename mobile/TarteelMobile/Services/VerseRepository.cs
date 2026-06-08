using System.Text.Json;
using TarteelClone.LocalRecitationCore.Utilities;
using Microsoft.Maui.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using TarteelMobile.Models;

namespace TarteelMobile.Services;

public interface IVerseRepository
{
    string DatabasePath { get; }
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
    Task<int> GetVerseCountAsync(CancellationToken cancellationToken = default);
    Task<Verse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Verse>> GetMemorizedVersesAsync(string? userKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VerseProgress>> GetProgressAsync(string? userKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> normalizedWords, CancellationToken cancellationToken = default);
    Task<JuzInfo?> GetJuzForVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default);
    Task<JuzInfo?> GetJuzAsync(int juzNum, CancellationToken cancellationToken = default);
    Task<SurahInfo?> GetSurahAsync(int surahNum, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int juzNum, CancellationToken cancellationToken = default);
    Task RecordRecitationAsync(
        string? userKey,
        int surahNum,
        int ayahNum,
        double masteryScore,
        CancellationToken cancellationToken = default);
}

public partial class LocalVerseRepository : IVerseRepository
{
    private const double EmaCurrentWeight = 0.7;
    private const double EmaNewWeight = 1.0 - EmaCurrentWeight;
    private static readonly JsonDocumentOptions ParseJsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private static readonly IReadOnlyList<ImportVerse> BuiltInFallbackVerses =
    [
        new ImportVerse(
            1,
            1,
            "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ",
            "بِسۡمِ ٱللَّهِ ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ",
            [new ImportTranslation("en", "In the name of Allah, the Entirely Merciful, the Especially Merciful.", "Saheeh International")]),
        new ImportVerse(
            1,
            2,
            "الْحَمْدُ لِلَّهِ رَبِّ الْعَالَمِينَ",
            "ٱلۡحَمۡدُ لِلَّهِ رَبِّ ٱلۡعَٰلَمِينَ",
            [new ImportTranslation("en", "[All] praise is [due] to Allah, Lord of the worlds -", "Saheeh International")]),
        new ImportVerse(
            1,
            3,
            "الرَّحْمَٰنِ الرَّحِيمِ",
            "ٱلرَّحۡمَٰنِ ٱلرَّحِيمِ",
            [new ImportTranslation("en", "The Entirely Merciful, the Especially Merciful,", "Saheeh International")]),
        new ImportVerse(
            1,
            4,
            "مَالِكِ يَوْمِ الدِّينِ",
            "مَٰلِكِ يَوۡمِ ٱلدِّينِ",
            [new ImportTranslation("en", "Sovereign of the Day of Recompense.", "Saheeh International")]),
        new ImportVerse(
            1,
            5,
            "إِيَّاكَ نَعْبُدُ وَإِيَّاكَ نَسْتَعِينُ",
            "إِيَّاكَ نَعۡبُدُ وَإِيَّاكَ نَسۡتَعِينُ",
            [new ImportTranslation("en", "It is You we worship and You we ask for help.", "Saheeh International")]),
        new ImportVerse(
            1,
            6,
            "اهْدِنَا الصِّرَاطَ الْمُسْتَقِيمَ",
            "ٱهۡدِنَا ٱلصِّرَٰطَ ٱلۡمُسۡتَقِيمَ",
            [new ImportTranslation("en", "Guide us to the straight path -", "Saheeh International")]),
        new ImportVerse(
            1,
            7,
            "صِرَاطَ الَّذِينَ أَنْعَمْتَ عَلَيْهِمْ غَيْرِ الْمَغْضُوبِ عَلَيْهِمْ وَلَا الضَّالِّينَ",
            "صِرَٰطَ ٱلَّذِينَ أَنۡعَمۡتَ عَلَيۡهِمۡ غَيۡرِ ٱلۡمَغۡضُوبِ عَلَيۡهِمۡ وَلَا ٱلضَّآلِّينَ",
            [new ImportTranslation("en", "The path of those upon whom You have bestowed favor, not of those who have earned [Your] anger or of those who are astray.", "Saheeh International")])
    ];
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS verses (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            surah_num INTEGER NOT NULL,
            ayah_num INTEGER NOT NULL,
            arabic_text TEXT NOT NULL,
            uthmani_text TEXT,
            UNIQUE(surah_num, ayah_num)
        );

        CREATE INDEX IF NOT EXISTS idx_verses_surah ON verses (surah_num);

        CREATE TABLE IF NOT EXISTS translations (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            verse_id INTEGER NOT NULL REFERENCES verses(id) ON DELETE CASCADE,
            language TEXT NOT NULL,
            text TEXT NOT NULL,
            translator TEXT NOT NULL DEFAULT ''
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_translations_verse_language_translator
            ON translations (verse_id, language, translator);
        CREATE INDEX IF NOT EXISTS idx_translations_verse ON translations (verse_id);
        CREATE INDEX IF NOT EXISTS idx_translations_language ON translations (language);

        CREATE TABLE IF NOT EXISTS memorization_progress (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_key TEXT NOT NULL,
            surah_num INTEGER NOT NULL,
            ayah_num INTEGER NOT NULL,
            mastery_score REAL NOT NULL DEFAULT 0.0,
            updated_at TEXT NOT NULL,
            UNIQUE(user_key, surah_num, ayah_num)
        );

        CREATE INDEX IF NOT EXISTS idx_progress_user_key ON memorization_progress (user_key);

        CREATE TABLE IF NOT EXISTS dataset_metadata (
            key TEXT PRIMARY KEY NOT NULL,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS juz (
            juz_num     INTEGER PRIMARY KEY,
            start_surah INTEGER NOT NULL,
            start_ayah  INTEGER NOT NULL,
            end_surah   INTEGER NOT NULL,
            end_ayah    INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS surahs (
            surah_num           INTEGER PRIMARY KEY,
            name_arabic         TEXT NOT NULL,
            name_english        TEXT NOT NULL,
            name_transliteration TEXT NOT NULL,
            revelation_type     TEXT NOT NULL,
            ayah_count          INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS word_index (
            word        TEXT NOT NULL,
            surah_num   INTEGER NOT NULL,
            ayah_num    INTEGER NOT NULL,
            position    INTEGER NOT NULL,
            PRIMARY KEY (word, surah_num, ayah_num, position)
        );

        CREATE INDEX IF NOT EXISTS idx_word_index_word ON word_index(word);
        """;

    private readonly LocalQuranDataOptions _options;
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _isInitialized;

    public LocalVerseRepository(
        IOptions<LocalQuranDataOptions> options,
        IAppDiagnosticsService diagnostics)
    {
        _options = options.Value;
        _diagnostics = diagnostics;

        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TarteelClone",
            "data");
        Directory.CreateDirectory(baseDir);

        var fileName = string.IsNullOrWhiteSpace(_options.DatabaseFileName)
            ? "quran-local.db"
            : _options.DatabaseFileName.Trim();
        DatabasePath = Path.Combine(baseDir, fileName);
    }

    public string DatabasePath { get; }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await using var connection = CreateOpenConnection();
            await ApplySchemaAsync(connection, cancellationToken);

            var verseCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM verses;", cancellationToken);
            if (verseCount == 0)
            {
                var importedCount = await ImportBootstrapDataAsync(connection, cancellationToken);
                await SetDatasetMetadataAsync(connection, "imported_verse_count", importedCount.ToString(), cancellationToken);
                _diagnostics.Info($"Imported {importedCount} verse(s) into local SQLite store.");
            }
            else
            {
                // Re-import if the asset has more verses than what was last recorded, so that
                // expanding the Quran data asset updates an existing installation.
                var storedImportCount = await GetDatasetMetadataAsync(connection, "imported_verse_count", cancellationToken);
                var assetVerseCount = await CountImportDatasetVersesAsync(cancellationToken);
                if (assetVerseCount > verseCount || (storedImportCount is not null && assetVerseCount > int.Parse(storedImportCount)))
                {
                    var importedCount = await ImportBootstrapDataAsync(connection, cancellationToken);
                    await SetDatasetMetadataAsync(connection, "imported_verse_count", importedCount.ToString(), cancellationToken);
                    _diagnostics.Info($"Re-imported {importedCount} verse(s) after dataset expansion (was {verseCount}).");
                }
                else
                {
                    _diagnostics.Info($"Local SQLite store already initialized with {verseCount} verse(s).");
                }
            }

            await SeedReferenceDataAsync(connection, cancellationToken);
            await BuildWordIndexIfEmptyAsync(connection, cancellationToken);

            _isInitialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<int> GetVerseCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        return await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM verses;", cancellationToken);
    }

    public async Task<Verse?> GetVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                v.surah_num,
                v.ayah_num,
                v.arabic_text,
                COALESCE(
                    (
                        SELECT tr.text
                        FROM translations tr
                        WHERE tr.verse_id = v.id
                          AND tr.language = @language
                        ORDER BY tr.id
                        LIMIT 1
                    ),
                    (
                        SELECT tr.text
                        FROM translations tr
                        WHERE tr.verse_id = v.id
                        ORDER BY tr.id
                        LIMIT 1
                    ),
                    ''
                ) AS translation
            FROM verses v
            WHERE v.surah_num = @surah_num
              AND v.ayah_num = @ayah_num
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@surah_num", surahNum);
        command.Parameters.AddWithValue("@ayah_num", ayahNum);
        command.Parameters.AddWithValue("@language", _options.DefaultTranslationLanguage);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Verse
        {
            SurahNum = reader.GetInt32(0),
            AyahNum = reader.GetInt32(1),
            ArabicText = reader.GetString(2),
            Translation = reader.GetString(3)
        };
    }

    public async Task<IReadOnlyList<Verse>> GetMemorizedVersesAsync(
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                p.surah_num,
                p.ayah_num,
                v.arabic_text,
                COALESCE(
                    (
                        SELECT tr.text
                        FROM translations tr
                        WHERE tr.verse_id = v.id
                          AND tr.language = @language
                        ORDER BY tr.id
                        LIMIT 1
                    ),
                    (
                        SELECT tr.text
                        FROM translations tr
                        WHERE tr.verse_id = v.id
                        ORDER BY tr.id
                        LIMIT 1
                    ),
                    ''
                ) AS translation
            FROM memorization_progress p
            INNER JOIN verses v
                ON v.surah_num = p.surah_num
               AND v.ayah_num = p.ayah_num
            WHERE p.user_key = @user_key
            ORDER BY p.surah_num, p.ayah_num;
            """;
        command.Parameters.AddWithValue("@language", _options.DefaultTranslationLanguage);
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));

        var verses = new List<Verse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            verses.Add(new Verse
            {
                SurahNum = reader.GetInt32(0),
                AyahNum = reader.GetInt32(1),
                ArabicText = reader.GetString(2),
                Translation = reader.GetString(3)
            });
        }

        return verses;
    }

    public async Task RecordRecitationAsync(
        string? userKey,
        int surahNum,
        int ayahNum,
        double masteryScore,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO memorization_progress (
                user_key,
                surah_num,
                ayah_num,
                mastery_score,
                updated_at
            )
            VALUES (
                @user_key,
                @surah_num,
                @ayah_num,
                @mastery_score,
                @updated_at
            )
            ON CONFLICT(user_key, surah_num, ayah_num)
            DO UPDATE SET
                mastery_score = (memorization_progress.mastery_score * @ema_current)
                              + (@mastery_score * @ema_new),
                updated_at = @updated_at;
            """;
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));
        command.Parameters.AddWithValue("@surah_num", surahNum);
        command.Parameters.AddWithValue("@ayah_num", ayahNum);
        command.Parameters.AddWithValue("@mastery_score", Math.Clamp(masteryScore, 0.0, 1.0));
        command.Parameters.AddWithValue("@updated_at", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@ema_current", EmaCurrentWeight);
        command.Parameters.AddWithValue("@ema_new", EmaNewWeight);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VerseProgress>> GetProgressAsync(
        string? userKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                p.surah_num,
                p.ayah_num,
                v.arabic_text,
                p.mastery_score,
                p.updated_at
            FROM memorization_progress p
            INNER JOIN verses v
                ON v.surah_num = p.surah_num
               AND v.ayah_num = p.ayah_num
            WHERE p.user_key = @user_key
            ORDER BY p.surah_num, p.ayah_num;
            """;
        command.Parameters.AddWithValue("@user_key", ResolveUserKey(userKey));

        var results = new List<VerseProgress>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new VerseProgress(
                SurahNum: reader.GetInt32(0),
                AyahNum: reader.GetInt32(1),
                ArabicText: reader.GetString(2),
                MasteryScore: reader.GetDouble(3),
                UpdatedAt: DateTimeOffset.TryParse(reader.GetString(4), out var dt) ? dt : DateTimeOffset.MinValue));
        }

        return results;
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        var connection = new SqliteConnection(connectionStringBuilder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static async Task ApplySchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = SchemaSql;
        await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SetDatasetMetadataAsync(SqliteConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dataset_metadata (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string?> GetDatasetMetadataAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM dataset_metadata WHERE key = @key LIMIT 1;";
        command.Parameters.AddWithValue("@key", key);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private async Task<int> CountImportDatasetVersesAsync(CancellationToken cancellationToken)
    {
        var importSource = await LoadImportDatasetAsync(cancellationToken);
        return importSource.Verses.Count > 0 ? importSource.Verses.Count : BuiltInFallbackVerses.Count;
    }

    private async Task<int> ImportBootstrapDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var importSource = await LoadImportDatasetAsync(cancellationToken);
        var records = importSource.Verses;
        if (records.Count == 0)
        {
            records = BuiltInFallbackVerses;
            _diagnostics.Warn("No import file yielded verse records; using built-in fallback verses.");
        }

        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var verse in records)
        {
            var verseId = await UpsertVerseAsync(connection, transaction, verse, cancellationToken);
            foreach (var translation in verse.Translations)
            {
                await UpsertTranslationAsync(connection, transaction, verseId, translation, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        _diagnostics.Info($"Bootstrap import source: {importSource.Source}.");
        return records.Count;
    }

    private async Task<(string Source, IReadOnlyList<ImportVerse> Verses)> LoadImportDatasetAsync(CancellationToken cancellationToken)
    {
        var externalPath = _options.ExternalImportFile?.Trim();
        if (!string.IsNullOrWhiteSpace(externalPath))
        {
            var resolvedPath = Path.GetFullPath(externalPath);
            if (File.Exists(resolvedPath))
            {
                await using var externalStream = File.OpenRead(resolvedPath);
                var parsed = await ParseImportVersesAsync(externalStream, cancellationToken);
                if (parsed.Count > 0)
                {
                    return ($"external-file:{resolvedPath}", parsed);
                }
            }
            else
            {
                _diagnostics.Warn($"Configured external Quran data file not found: {resolvedPath}");
            }
        }

        var primaryAsset = await TryReadAssetVersesAsync(_options.PreferredAssetImportFile, cancellationToken);
        if (primaryAsset.Count > 0)
        {
            return ($"asset:{_options.PreferredAssetImportFile}", primaryAsset);
        }

        var seedAsset = await TryReadAssetVersesAsync(_options.FallbackSeedAssetImportFile, cancellationToken);
        if (seedAsset.Count > 0)
        {
            return ($"asset:{_options.FallbackSeedAssetImportFile}", seedAsset);
        }

        return ("built-in-fallback", []);
    }

    private async Task<IReadOnlyList<ImportVerse>> TryReadAssetVersesAsync(string assetPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return [];
        }

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            var parsed = await ParseImportVersesAsync(stream, cancellationToken);
            if (parsed.Count == 0)
            {
                _diagnostics.Warn($"Asset '{assetPath}' was found but had no usable verses.");
            }

            return parsed;
        }
        catch (FileNotFoundException)
        {
            _diagnostics.Warn($"Quran data asset not found: {assetPath}");
            return [];
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Failed to load Quran asset '{assetPath}'.", ex);
            return [];
        }
    }

    private async Task<IReadOnlyList<ImportVerse>> ParseImportVersesAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var jsonDocument = await JsonDocument.ParseAsync(
                stream,
                ParseJsonOptions,
                cancellationToken);

            var verses = new List<ImportVerse>();
            foreach (var verseElement in ExtractVerseElements(jsonDocument.RootElement))
            {
                if (TryParseVerse(verseElement, out var verse))
                {
                    verses.Add(verse);
                }
            }

            return verses;
        }
        catch (JsonException ex)
        {
            _diagnostics.Error("Failed to parse Quran import JSON.", ex);
            return [];
        }
    }

    private static IEnumerable<JsonElement> ExtractVerseElements(JsonElement rootElement)
    {
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in rootElement.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        if (rootElement.ValueKind == JsonValueKind.Object &&
            TryGetProperty(rootElement, "verses", out var versesElement) &&
            versesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in versesElement.EnumerateArray())
            {
                yield return element;
            }
        }
    }

    private bool TryParseVerse(JsonElement verseElement, out ImportVerse verse)
    {
        verse = default!;
        if (verseElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadInt(verseElement, ["surah_num", "surahNum", "surah"], out var surahNum) ||
            !TryReadInt(verseElement, ["ayah_num", "ayahNum", "ayah"], out var ayahNum))
        {
            return false;
        }

        var arabicText = ReadString(verseElement, ["arabic_text", "arabicText", "text"]);
        if (string.IsNullOrWhiteSpace(arabicText))
        {
            return false;
        }

        var uthmaniText = ReadString(verseElement, ["uthmani_text", "uthmaniText"]);
        var translations = ParseTranslations(verseElement);

        verse = new ImportVerse(surahNum, ayahNum, arabicText, uthmaniText, translations);
        return true;
    }

    private IReadOnlyList<ImportTranslation> ParseTranslations(JsonElement verseElement)
    {
        var translations = new List<ImportTranslation>();

        if (TryGetProperty(verseElement, "translations", out var translationsElement) &&
            translationsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var translationElement in translationsElement.EnumerateArray())
            {
                if (translationElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var language = ReadString(translationElement, ["language", "lang"]);
                if (string.IsNullOrWhiteSpace(language))
                {
                    language = _options.DefaultTranslationLanguage;
                }

                var text = ReadString(translationElement, ["text", "translation"]);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var translator = ReadString(translationElement, ["translator", "author", "source"]);
                if (string.IsNullOrWhiteSpace(translator))
                {
                    translator = _options.DefaultTranslator;
                }

                translations.Add(new ImportTranslation(language, text, translator));
            }
        }

        if (translations.Count > 0)
        {
            return translations;
        }

        var inlineTranslation = ReadString(verseElement, ["translation", "translation_text"]);
        if (string.IsNullOrWhiteSpace(inlineTranslation))
        {
            return [];
        }

        return
        [
            new ImportTranslation(
                _options.DefaultTranslationLanguage,
                inlineTranslation,
                _options.DefaultTranslator)
        ];
    }

    private static bool TryReadInt(JsonElement element, IEnumerable<string> propertyNames, out int value)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                continue;
            }

            if (propertyValue.ValueKind == JsonValueKind.Number &&
                propertyValue.TryGetInt32(out value))
            {
                return true;
            }

            if (propertyValue.ValueKind == JsonValueKind.String &&
                int.TryParse(propertyValue.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string ReadString(JsonElement element, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(element, propertyName, out var propertyValue))
            {
                continue;
            }

            if (propertyValue.ValueKind == JsonValueKind.String)
            {
                var text = propertyValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propertyValue = property.Value;
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static async Task<int> UpsertVerseAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ImportVerse verse,
        CancellationToken cancellationToken)
    {
        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.Transaction = transaction;
        upsertCommand.CommandText = """
            INSERT INTO verses (
                surah_num,
                ayah_num,
                arabic_text,
                uthmani_text
            )
            VALUES (
                @surah_num,
                @ayah_num,
                @arabic_text,
                @uthmani_text
            )
            ON CONFLICT(surah_num, ayah_num)
            DO UPDATE SET
                arabic_text = excluded.arabic_text,
                uthmani_text = excluded.uthmani_text;
            """;
        upsertCommand.Parameters.AddWithValue("@surah_num", verse.SurahNum);
        upsertCommand.Parameters.AddWithValue("@ayah_num", verse.AyahNum);
        upsertCommand.Parameters.AddWithValue("@arabic_text", verse.ArabicText);
        upsertCommand.Parameters.AddWithValue("@uthmani_text", string.IsNullOrWhiteSpace(verse.UthmaniText) ? DBNull.Value : verse.UthmaniText);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var getIdCommand = connection.CreateCommand();
        getIdCommand.Transaction = transaction;
        getIdCommand.CommandText = """
            SELECT id
            FROM verses
            WHERE surah_num = @surah_num
              AND ayah_num = @ayah_num
            LIMIT 1;
            """;
        getIdCommand.Parameters.AddWithValue("@surah_num", verse.SurahNum);
        getIdCommand.Parameters.AddWithValue("@ayah_num", verse.AyahNum);

        var verseId = await getIdCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(verseId);
    }

    private static async Task UpsertTranslationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int verseId,
        ImportTranslation translation,
        CancellationToken cancellationToken)
    {
        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.Transaction = transaction;
        upsertCommand.CommandText = """
            INSERT INTO translations (
                verse_id,
                language,
                text,
                translator
            )
            VALUES (
                @verse_id,
                @language,
                @text,
                @translator
            )
            ON CONFLICT(verse_id, language, translator)
            DO UPDATE SET
                text = excluded.text;
            """;
        upsertCommand.Parameters.AddWithValue("@verse_id", verseId);
        upsertCommand.Parameters.AddWithValue("@language", translation.Language);
        upsertCommand.Parameters.AddWithValue("@text", translation.Text);
        upsertCommand.Parameters.AddWithValue("@translator", translation.Translator);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ExecuteScalarIntAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private string ResolveUserKey(string? userKey)
    {
        var normalized = userKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = _options.DefaultUserKey;
        }

        return normalized!;
    }

    private sealed record ImportVerse(
        int SurahNum,
        int AyahNum,
        string ArabicText,
        string UthmaniText,
        IReadOnlyList<ImportTranslation> Translations);


    private async Task SeedReferenceDataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var juzCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM juz;", cancellationToken);
        if (juzCount == 0)
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var juz in BuiltInJuzData)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT OR IGNORE INTO juz (juz_num, start_surah, start_ayah, end_surah, end_ayah) VALUES (@num, @ss, @sa, @es, @ea);";
                cmd.Parameters.AddWithValue("@num", juz.JuzNum);
                cmd.Parameters.AddWithValue("@ss", juz.StartSurah);
                cmd.Parameters.AddWithValue("@sa", juz.StartAyah);
                cmd.Parameters.AddWithValue("@es", juz.EndSurah);
                cmd.Parameters.AddWithValue("@ea", juz.EndAyah);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        var surahCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM surahs;", cancellationToken);
        if (surahCount == 0)
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            foreach (var surah in BuiltInSurahData)
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT OR IGNORE INTO surahs (surah_num, name_arabic, name_english, name_transliteration, revelation_type, ayah_count) VALUES (@num, @na, @ne, @nt, @rt, @ac);";
                cmd.Parameters.AddWithValue("@num", surah.SurahNum);
                cmd.Parameters.AddWithValue("@na", surah.NameArabic);
                cmd.Parameters.AddWithValue("@ne", surah.NameEnglish);
                cmd.Parameters.AddWithValue("@nt", surah.NameTransliteration);
                cmd.Parameters.AddWithValue("@rt", surah.RevelationType);
                cmd.Parameters.AddWithValue("@ac", surah.AyahCount);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task BuildWordIndexIfEmptyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var indexCount = await ExecuteScalarIntAsync(connection, "SELECT COUNT(*) FROM word_index;", cancellationToken);
        if (indexCount > 0)
        {
            return;
        }

        await using var readerCmd = connection.CreateCommand();
        readerCmd.CommandText = "SELECT surah_num, ayah_num, arabic_text FROM verses ORDER BY surah_num, ayah_num;";
        await using var reader = await readerCmd.ExecuteReaderAsync(cancellationToken);

        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var batchCount = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            var surahNum = reader.GetInt32(0);
            var ayahNum = reader.GetInt32(1);
            var arabicText = reader.GetString(2);

            var words = ArabicNormalizer.TokenizeAndNormalize(arabicText);
            for (var pos = 0; pos < words.Length; pos++)
            {
                var word = words[pos];
                if (word.Length <= 1)
                {
                    continue;
                }

                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = "INSERT OR IGNORE INTO word_index (word, surah_num, ayah_num, position) VALUES (@w, @s, @a, @p);";
                insertCmd.Parameters.AddWithValue("@w", word);
                insertCmd.Parameters.AddWithValue("@s", surahNum);
                insertCmd.Parameters.AddWithValue("@a", ayahNum);
                insertCmd.Parameters.AddWithValue("@p", pos);
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            batchCount++;
            if (batchCount % 500 == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Verse>> GetAllVersesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                v.surah_num,
                v.ayah_num,
                v.arabic_text,
                COALESCE(
                    (SELECT tr.text FROM translations tr WHERE tr.verse_id = v.id AND tr.language = @language ORDER BY tr.id LIMIT 1),
                    (SELECT tr.text FROM translations tr WHERE tr.verse_id = v.id ORDER BY tr.id LIMIT 1),
                    ''
                ) AS translation
            FROM verses v
            ORDER BY v.surah_num, v.ayah_num;";
        command.Parameters.AddWithValue("@language", _options.DefaultTranslationLanguage);

        var verses = new List<Verse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            verses.Add(new Verse
            {
                SurahNum = reader.GetInt32(0),
                AyahNum = reader.GetInt32(1),
                ArabicText = reader.GetString(2),
                Translation = reader.GetString(3)
            });
        }

        return verses;
    }

    public async Task<IReadOnlyList<Verse>> GetVersesByWordsAsync(IReadOnlyList<string> normalizedWords, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (normalizedWords.Count == 0)
        {
            return [];
        }

        await using var connection = CreateOpenConnection();

        var placeholders = string.Join(", ", normalizedWords.Select((_, i) => "@w" + i));

        await using var lookupCmd = connection.CreateCommand();
        lookupCmd.CommandText = "SELECT DISTINCT wi.surah_num, wi.ayah_num FROM word_index wi WHERE wi.word IN (" + placeholders + ") ORDER BY wi.surah_num, wi.ayah_num LIMIT 500;";

        for (var i = 0; i < normalizedWords.Count; i++)
        {
            lookupCmd.Parameters.AddWithValue("@w" + i, normalizedWords[i]);
        }

        var candidates = new List<(int Surah, int Ayah)>();
        await using var lookupReader = await lookupCmd.ExecuteReaderAsync(cancellationToken);
        while (await lookupReader.ReadAsync(cancellationToken))
        {
            candidates.Add((lookupReader.GetInt32(0), lookupReader.GetInt32(1)));
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var verses = new List<Verse>();
        foreach (var (surah, ayah) in candidates)
        {
            await using var verseCmd = connection.CreateCommand();
            verseCmd.CommandText = @"
                SELECT
                    v.surah_num,
                    v.ayah_num,
                    v.arabic_text,
                    COALESCE(
                        (SELECT tr.text FROM translations tr WHERE tr.verse_id = v.id AND tr.language = @language ORDER BY tr.id LIMIT 1),
                        (SELECT tr.text FROM translations tr WHERE tr.verse_id = v.id ORDER BY tr.id LIMIT 1),
                        ''
                    ) AS translation
                FROM verses v
                WHERE v.surah_num = @surah_num AND v.ayah_num = @ayah_num
                LIMIT 1;";
            verseCmd.Parameters.AddWithValue("@language", _options.DefaultTranslationLanguage);
            verseCmd.Parameters.AddWithValue("@surah_num", surah);
            verseCmd.Parameters.AddWithValue("@ayah_num", ayah);

            await using var verseReader = await verseCmd.ExecuteReaderAsync(cancellationToken);
            if (await verseReader.ReadAsync(cancellationToken))
            {
                verses.Add(new Verse
                {
                    SurahNum = verseReader.GetInt32(0),
                    AyahNum = verseReader.GetInt32(1),
                    ArabicText = verseReader.GetString(2),
                    Translation = verseReader.GetString(3)
                });
            }
        }

        return verses;
    }

    public async Task<JuzInfo?> GetJuzForVerseAsync(int surahNum, int ayahNum, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT juz_num, start_surah, start_ayah, end_surah, end_ayah
            FROM juz
            WHERE (@sn > start_surah AND @sn < end_surah)
               OR (@sn = start_surah AND @an >= start_ayah)
               OR (@sn = end_surah AND @an <= end_ayah)
            LIMIT 1;";

        try
        {
            var resolvedSurahNum = surahNum;
            command.Parameters.AddWithValue("@a0", resolvedSurahNum);
            command.Parameters.AddWithValue("@a1", ayahNum);
            command.Parameters.AddWithValue("@a2", resolvedSurahNum);
            command.Parameters.AddWithValue("@a3", ayahNum);
            command.Parameters.AddWithValue("@a4", resolvedSurahNum);
            command.Parameters.AddWithValue("@a5", ayahNum);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new JuzInfo(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
        }
        catch
        {
            return null;
        }
    }

    public async Task<JuzInfo?> GetJuzAsync(int juzNum, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT juz_num, start_surah, start_ayah, end_surah, end_ayah FROM juz WHERE juz_num = @num LIMIT 1;";
        command.Parameters.AddWithValue("@num", juzNum);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new JuzInfo(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    public async Task<SurahInfo?> GetSurahAsync(int surahNum, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT surah_num, name_arabic, name_english, name_transliteration, revelation_type, ayah_count FROM surahs WHERE surah_num = @num LIMIT 1;";
        command.Parameters.AddWithValue("@num", surahNum);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SurahInfo(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5));
    }

    public async Task<IReadOnlyList<JuzInfo>> GetAllJuzAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT juz_num, start_surah, start_ayah, end_surah, end_ayah FROM juz ORDER BY juz_num;";

        var list = new List<JuzInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new JuzInfo(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)));
        }

        return list;
    }

    public async Task<IReadOnlyList<SurahInfo>> GetAllSurahsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT surah_num, name_arabic, name_english, name_transliteration, revelation_type, ayah_count FROM surahs ORDER BY surah_num;";

        var list = new List<SurahInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SurahInfo(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5)));
        }

        return list;
    }

    public async Task<IReadOnlyList<SurahInfo>> GetSurahsByJuzAsync(int juzNum, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var connection = CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT DISTINCT s.surah_num, s.name_arabic, s.name_english, s.name_transliteration, s.revelation_type, s.ayah_count
            FROM surahs s
            JOIN juz j ON s.surah_num BETWEEN j.start_surah AND j.end_surah
            WHERE j.juz_num = @juzNum
            ORDER BY s.surah_num;";
        command.Parameters.AddWithValue("@juzNum", juzNum);

        var list = new List<SurahInfo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new SurahInfo(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5)));
        }

        return list;
    }

    private sealed record ImportTranslation(
        string Language,
        string Text,
        string Translator);
}
