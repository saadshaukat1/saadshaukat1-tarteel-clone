using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TarteelClone.QuranEngine.Services;

namespace TarteelClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuranController : ControllerBase
{
    private readonly IVerseMatchingService _matching;

    public QuranController(IVerseMatchingService matching)
        => _matching = matching;

    /// <summary>Returns a specific verse by surah and ayah number.</summary>
    [HttpGet("{surahNum}/{ayahNum}")]
    public async Task<IActionResult> GetVerse(int surahNum, int ayahNum,
        CancellationToken ct)
    {
        var verse = await _matching.GetVerseAsync(surahNum, ayahNum, ct);
        return verse is null ? NotFound() : Ok(verse);
    }

    /// <summary>Returns all verses in a surah.</summary>
    [HttpGet("{surahNum}")]
    public async Task<IActionResult> GetSurah(int surahNum, CancellationToken ct)
    {
        var verses = await _matching.GetSurahAsync(surahNum, ct);
        return Ok(verses);
    }

    /// <summary>Matches a transcribed Arabic text against the Quran.</summary>
    [HttpPost("match")]
    [Authorize]
    public async Task<IActionResult> MatchText([FromBody] MatchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ArabicText))
            return BadRequest("ArabicText is required.");

        var result = await _matching.MatchAsync(request.ArabicText, ct);
        return Ok(result);
    }

    public record MatchRequest(string ArabicText);
}
