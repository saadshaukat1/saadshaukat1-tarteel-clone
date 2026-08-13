namespace TarteelMobile.Services;

public interface ISessionService
{
    bool IsAuthenticated { get; }
    string? CurrentUserEmail { get; }
    Task<bool>  LoginAsync(string email, string password);
    Task<bool>  RegisterAsync(string email, string password);
    Task LogoutAsync();
}

public sealed class LocalSessionService : ISessionService
{
    private readonly IAppDiagnosticsService _diagnostics;
    private readonly IVerseRepository? _verses;

    public LocalSessionService(IAppDiagnosticsService diagnostics, IVerseRepository? verses = null)
    {
        _diagnostics = diagnostics;
        _verses = verses;
    }

    public bool IsAuthenticated { get; private set; }
    public string? CurrentUserEmail { get; private set; }

    public Task<bool> LoginAsync(string email, string password)
        => TryAuthenticateAsync(email, password, "login");

    public Task<bool> RegisterAsync(string email, string password)
        => TryAuthenticateAsync(email, password, "registration");

    public Task LogoutAsync()
    {
        if (IsAuthenticated)
        {
            _diagnostics.Info($"User '{CurrentUserEmail}' logged out from local session.");
        }

        IsAuthenticated = false;
        CurrentUserEmail = null;
        return Task.CompletedTask;
    }

    private async Task<bool> TryAuthenticateAsync(string email, string password, string operation)
    {
        var normalizedEmail = email.Trim();
        if (!IsValidEmail(normalizedEmail) || password.Trim().Length < 4)
        {
            _diagnostics.Warn($"Rejected local {operation} attempt due to invalid credentials format.");
            return false;
        }

        IsAuthenticated = true;
        CurrentUserEmail = normalizedEmail;
        _diagnostics.Info($"Accepted local {operation} for '{normalizedEmail}'.");

        // Persist a profile row (created on first login, last-active touched on
        // subsequent ones) so the dashboard can surface user state offline.
        if (_verses is not null)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var existing = await _verses.GetUserProfileAsync(normalizedEmail);
                await _verses.CreateUserProfileAsync(
                    existing is null
                        ? new TarteelClone.UserService.UserProfile(normalizedEmail, normalizedEmail, normalizedEmail, now, now)
                        : existing with { LastActiveAt = now });
            }
            catch (Exception ex)
            {
                _diagnostics.Warn($"Could not persist profile for '{normalizedEmail}': {ex.Message}");
            }
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return email.Contains('@', StringComparison.Ordinal) &&
               email.IndexOf(' ') < 0 &&
               email.Contains('.', StringComparison.Ordinal);
    }
}
