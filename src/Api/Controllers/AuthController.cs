using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TarteelClone.UserService.Services;

namespace TarteelClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registers a new user and returns a JWT.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req,
        CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(req.Email, req.Password, ct);
        return result.Succeeded ? Ok(new { result.Token }) : BadRequest(result.Errors);
    }

    /// <summary>Authenticates a user and returns a JWT.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req,
        CancellationToken ct)
    {
        var result = await _auth.LoginAsync(req.Email, req.Password, ct);
        return result.Succeeded ? Ok(new { result.Token }) : Unauthorized(result.Errors);
    }

    public record RegisterRequest(string Email, string Password);
    public record LoginRequest(string Email, string Password);
}
