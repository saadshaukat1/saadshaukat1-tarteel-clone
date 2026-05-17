using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TarteelClone.UserService.Data;
using TarteelClone.UserService.Models;

namespace TarteelClone.UserService.Services;

public class AuthService : IAuthService
{
    private readonly UserDbContext  _db;
    private readonly IConfiguration _config;

    public AuthService(UserDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password,
        CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return new AuthResult(false, null, ["Email already registered."]);

        var user = new User
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new AuthResult(true, GenerateToken(user), []);
    }

    public async Task<AuthResult> LoginAsync(string email, string password,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, null, ["Invalid email or password."]);

        return new AuthResult(true, GenerateToken(user), []);
    }

    // ── JWT factory ───────────────────────────────────────────────────────────

    private string GenerateToken(User user)
    {
        var key     = _config["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key missing.");
        var issuer   = _config["Jwt:Issuer"]  ?? "TarteelCloneApi";
        var audience = _config["Jwt:Audience"] ?? "TarteelCloneClients";
        var expiry   = int.Parse(_config["Jwt:ExpiryMinutes"] ?? "60");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,          user.Email)
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
