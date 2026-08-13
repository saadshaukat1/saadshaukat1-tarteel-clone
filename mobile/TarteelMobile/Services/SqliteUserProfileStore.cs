using Microsoft.Data.Sqlite;
using TarteelClone.UserService;

namespace TarteelMobile.Services;

/// <summary>
/// SQLite-backed user profile + preferences store. Shares the app's data
/// directory so profiles survive restarts and match the repository's schema.
/// </summary>
public sealed class SqliteUserProfileStore : IUserProfileStore
{
    private readonly string _databasePath;
    private readonly object _sync = new();
    private bool _initialized;

    public SqliteUserProfileStore()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TarteelClone",
            "data");
        Directory.CreateDirectory(baseDir);
        _databasePath = Path.Combine(baseDir, "quran-local.db");
    }

    /// <summary>For tests: point at an isolated database file.</summary>
    public SqliteUserProfileStore(string databasePath)
    {
        _databasePath = databasePath;
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public Task<UserProfile?> GetProfileAsync(string userKey, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT user_key, display_name, email, created_at, last_active_at FROM user_profiles WHERE user_key = @user_key LIMIT 1;";
        command.Parameters.AddWithValue("@user_key", userKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<UserProfile?>(null);
        }

        return Task.FromResult<UserProfile?>(new UserProfile(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            DateTimeOffset.Parse(reader.GetString(3)), DateTimeOffset.Parse(reader.GetString(4))));
    }

    public Task<UserProfile> CreateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO user_profiles (user_key, display_name, email, created_at, last_active_at)
            VALUES (@user_key, @display_name, @email, @created_at, @last_active_at)
            ON CONFLICT(user_key) DO UPDATE SET
                display_name = @display_name, email = @email, last_active_at = @last_active_at;
            """;
        command.Parameters.AddWithValue("@user_key", profile.UserKey);
        command.Parameters.AddWithValue("@display_name", profile.DisplayName);
        command.Parameters.AddWithValue("@email", profile.Email);
        command.Parameters.AddWithValue("@created_at", profile.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@last_active_at", profile.LastActiveAt.ToString("O"));
        command.ExecuteNonQuery();
        return Task.FromResult(profile);
    }

    public Task<UserPreferences> GetPreferencesAsync(string userKey, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT user_key, goal, daily_new_lessons, daily_reviews, enable_audio_feedback, updated_at FROM user_preferences WHERE user_key = @user_key LIMIT 1;";
        command.Parameters.AddWithValue("@user_key", userKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult(new UserPreferences(userKey, MemorizationGoal.Casual, 1, 5, false, DateTimeOffset.UtcNow));
        }

        return Task.FromResult(new UserPreferences(
            reader.GetString(0), (MemorizationGoal)reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4) != 0, DateTimeOffset.Parse(reader.GetString(5))));
    }

    public Task<UserPreferences> SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO user_preferences (user_key, goal, daily_new_lessons, daily_reviews, enable_audio_feedback, updated_at)
            VALUES (@user_key, @goal, @daily_new, @daily_reviews, @audio, @updated_at)
            ON CONFLICT(user_key) DO UPDATE SET
                goal = @goal, daily_new_lessons = @daily_new, daily_reviews = @daily_reviews,
                enable_audio_feedback = @audio, updated_at = @updated_at;
            """;
        command.Parameters.AddWithValue("@user_key", preferences.UserKey);
        command.Parameters.AddWithValue("@goal", (int)preferences.Goal);
        command.Parameters.AddWithValue("@daily_new", preferences.DailyNewLessons);
        command.Parameters.AddWithValue("@daily_reviews", preferences.DailyReviews);
        command.Parameters.AddWithValue("@audio", preferences.EnableAudioFeedback ? 1 : 0);
        command.Parameters.AddWithValue("@updated_at", preferences.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
        return Task.FromResult(preferences);
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_sync)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS user_profiles (
                    user_key TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    email TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    last_active_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS user_preferences (
                    user_key TEXT PRIMARY KEY,
                    goal INTEGER NOT NULL DEFAULT 0,
                    daily_new_lessons INTEGER NOT NULL DEFAULT 1,
                    daily_reviews INTEGER NOT NULL DEFAULT 5,
                    enable_audio_feedback INTEGER NOT NULL DEFAULT 0,
                    updated_at TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            _initialized = true;
        }
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }
}
