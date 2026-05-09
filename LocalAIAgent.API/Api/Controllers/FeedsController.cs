using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application;
using LocalAIAgent.Application.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FeedsController(
    UserContext context,
    IFeedCatalog feedCatalog) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<ActionResult<List<FeedDto>>> GetFeeds(int userId)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        HashSet<string> disabled = new(preferences.DisabledFeedSources, StringComparer.OrdinalIgnoreCase);

        List<FeedDto> result = [.. feedCatalog.GetAllFeeds()
            .Select(f => new FeedDto
            {
                ClientName = f.ClientName,
                DisplayName = f.DisplayName,
                Language = f.Language,
                LanguageName = Languages.GetDisplayName(f.Language),
                Enabled = !disabled.Contains(f.ClientName),
            })];

        return Ok(result);
    }

    [HttpPost("Toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleFeedDto dto)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        bool isKnownFeed = feedCatalog.GetAllFeeds().Any(f => f.ClientName == dto.ClientName);
        if (!isKnownFeed)
            return BadRequest($"Unknown feed: {dto.ClientName}");

        HashSet<string> disabled = new(preferences.DisabledFeedSources, StringComparer.OrdinalIgnoreCase);

        if (dto.Enabled)
            disabled.Remove(dto.ClientName);
        else
            disabled.Add(dto.ClientName);

        preferences.DisabledFeedSources = [.. disabled];
        await context.SaveChangesAsync();
        return Ok();
    }
}
