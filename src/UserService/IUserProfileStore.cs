namespace TarteelClone.UserService;

public sealed record UserProfile(
    string UserKey,
    string DisplayName,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActiveAt);

public enum MemorizationGoal
{
    Casual,
    Consistent,
    Intensive
}

public sealed record UserPreferences(
    string UserKey,
    MemorizationGoal Goal,
    int DailyNewLessons,
    int DailyReviews,
    bool EnableAudioFeedback,
    DateTimeOffset UpdatedAt);

public interface IUserProfileStore
{
    Task<UserProfile?> GetProfileAsync(string userKey, CancellationToken cancellationToken = default);
    Task<UserProfile> CreateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default);
    Task<UserPreferences> GetPreferencesAsync(string userKey, CancellationToken cancellationToken = default);
    Task<UserPreferences> SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}

public sealed class InMemoryUserProfileStore : IUserProfileStore
{
    private readonly Dictionary<string, UserProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UserPreferences> _preferences = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserProfile?> GetProfileAsync(string userKey, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(userKey, out var profile);
        return Task.FromResult(profile);
    }

    public Task<UserProfile> CreateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.UserKey] = profile;
        return Task.FromResult(profile);
    }

    public Task<UserPreferences> GetPreferencesAsync(string userKey, CancellationToken cancellationToken = default)
    {
        if (!_preferences.TryGetValue(userKey, out var prefs))
        {
            prefs = new UserPreferences(userKey, MemorizationGoal.Casual, 1, 5, false, DateTimeOffset.UtcNow);
            _preferences[userKey] = prefs;
        }
        return Task.FromResult(prefs);
    }

    public Task<UserPreferences> SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        _preferences[preferences.UserKey] = preferences;
        return Task.FromResult(preferences);
    }
}
