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
    IFeedCatalog feedCatalog,
    ICustomFeedRepository customFeedRepository) : ControllerBase
{
    private const string CustomClientNamePrefix = "custom:";

    [HttpGet("{userId}")]
    public async Task<ActionResult<List<FeedDto>>> GetFeeds(int userId)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        HashSet<string> disabledBuiltIns = new(preferences.DisabledFeedSources, StringComparer.OrdinalIgnoreCase);

        List<FeedDto> result = [.. feedCatalog.GetAllFeeds()
            .Select(f => new FeedDto
            {
                ClientName = f.ClientName,
                DisplayName = f.DisplayName,
                Language = f.Language,
                LanguageName = Languages.GetDisplayName(f.Language),
                Enabled = !disabledBuiltIns.Contains(f.ClientName),
                IsCustom = false,
            })];

        List<CustomFeedDescriptor> customs = await customFeedRepository.GetForUserAsync(preferences.Id);
        result.AddRange(customs.Select(c => new FeedDto
        {
            ClientName = CustomClientNamePrefix + c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DisplayName = c.DisplayName,
            Language = c.Language,
            LanguageName = Languages.GetDisplayName(c.Language),
            Enabled = c.Enabled,
            IsCustom = true,
            CustomFeedId = c.Id,
            Url = c.Url,
            LastFetchErrorMessage = c.LastFetchErrorMessage,
        }));

        return Ok(result);
    }

    [HttpPost("Toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleFeedDto dto)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        // Custom-feed toggles route through the repository; built-ins flip the disabled-list.
        if (dto.ClientName.StartsWith(CustomClientNamePrefix, StringComparison.Ordinal))
        {
            string idText = dto.ClientName[CustomClientNamePrefix.Length..];
            if (!int.TryParse(idText, System.Globalization.CultureInfo.InvariantCulture, out int customFeedId))
                return BadRequest($"Invalid custom feed id: {dto.ClientName}");

            bool found = await customFeedRepository.SetEnabledAsync(preferences.Id, customFeedId, dto.Enabled);
            return found ? Ok() : NotFound("Custom feed not found.");
        }

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

    [HttpPost("Custom")]
    public async Task<ActionResult<FeedDto>> AddCustom([FromBody] AddCustomFeedDto dto)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest("Url must be an absolute http(s) URL.");

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return BadRequest("DisplayName is required.");

        if (!Languages.IsSupported(dto.Language))
            return BadRequest($"Unsupported language code: {dto.Language}");

        try
        {
            CustomFeedDescriptor created = await customFeedRepository.AddAsync(
                preferences.Id, dto.Url, dto.DisplayName.Trim(), dto.Language);

            return Ok(new FeedDto
            {
                ClientName = CustomClientNamePrefix + created.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DisplayName = created.DisplayName,
                Language = created.Language,
                LanguageName = Languages.GetDisplayName(created.Language),
                Enabled = created.Enabled,
                IsCustom = true,
                CustomFeedId = created.Id,
                Url = created.Url,
                LastFetchErrorMessage = created.LastFetchErrorMessage,
            });
        }
        catch (DbUpdateException)
        {
            // Composite unique on (UserPreferencesId, Url) — the user already has this feed.
            return Conflict("This URL is already in your feed list.");
        }
    }

    [HttpDelete("Custom/{customFeedId}")]
    public async Task<IActionResult> RemoveCustom(int customFeedId, [FromQuery] int userId)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences is null)
            return NotFound("User preferences not found.");

        bool removed = await customFeedRepository.RemoveAsync(preferences.Id, customFeedId);
        return removed ? Ok() : NotFound("Custom feed not found.");
    }
}
