namespace TarteelClone.UserService.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
}

public record AuthResult(bool Succeeded, string? Token, IEnumerable<string> Errors);
