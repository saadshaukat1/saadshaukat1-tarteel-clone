using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TarteelClone.UserService.Services;

namespace TarteelClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProgressController : ControllerBase
{
    private readonly IProgressService _progress;

    public ProgressController(IProgressService progress) => _progress = progress;

    /// <summary>Returns memorization progress for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetProgress(CancellationToken ct)
    {
        var userId = GetUserId();
        var progress = await _progress.GetProgressAsync(userId, ct);
        return Ok(progress);
    }

    /// <summary>Records a recitation result for a specific verse.</summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordRecitation(
        [FromBody] RecitationResultRequest req, CancellationToken ct)
    {
        var userId = GetUserId();
        await _progress.RecordRecitationAsync(userId, req.SurahNum, req.AyahNum,
            req.MasteryScore, ct);
        return NoContent();
    }

    private int GetUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID claim missing."));

    public record RecitationResultRequest(int SurahNum, int AyahNum, double MasteryScore);
}
