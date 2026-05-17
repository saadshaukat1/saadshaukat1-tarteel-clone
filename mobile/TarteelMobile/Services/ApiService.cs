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

    public bool IsAuthenticated { get; private set; }
    public string? CurrentUserEmail { get; private set; }

    public LocalSessionService(IAppDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public Task<bool> LoginAsync(string email, string password)
        => Task.FromResult(TryAuthenticate(email, password, "login"));

    public Task<bool> RegisterAsync(string email, string password)
        => Task.FromResult(TryAuthenticate(email, password, "registration"));

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

    private bool TryAuthenticate(string email, string password, string operation)
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
