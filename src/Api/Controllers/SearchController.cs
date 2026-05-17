using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TarteelClone.SearchService.Services;

namespace TarteelClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IQuranSearchService _search;

    public SearchController(IQuranSearchService search) => _search = search;

    /// <summary>Searches the Quran by keyword or Arabic text query.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q,
        [FromQuery] string? lang = "en", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Query parameter 'q' is required.");

        var results = await _search.SearchAsync(q, lang ?? "en", ct);
        return Ok(results);
    }
}
