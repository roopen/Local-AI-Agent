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
    ICustomFeedRepository customFeedRepository,
    IFeedValidator feedValidator) : ControllerBase
{
    private const string CustomClientNamePrefix = "custom:";

    [HttpGet("Languages")]
    [AllowAnonymous]
    public ActionResult<List<LanguageOptionDto>> GetSupportedLanguages()
    {
        return Ok(Languages.GetAllSupported()
            .Select(l => new LanguageOptionDto { Code = l.Code, Name = l.Name })
            .ToList());
    }

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
            Urls = c.Urls,
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
    public async Task<ActionResult<FeedDto>> AddCustom([FromBody] AddCustomFeedDto dto, CancellationToken cancellationToken)
    {
        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId, cancellationToken);
        if (preferences is null)
            return NotFound("User preferences not found.");

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return BadRequest(new AddCustomFeedErrorDto { Message = "Display name is required." });

        if (!Languages.IsSupported(dto.Language))
            return BadRequest(new AddCustomFeedErrorDto { Message = $"Unsupported language code: {dto.Language}" });

        // Trim and dedupe before validation so the user gets feedback on the URLs they actually submitted.
        List<string> normalizedUrls = [.. dto.Urls
            .Select(u => u?.Trim() ?? string.Empty)
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal)];

        if (normalizedUrls.Count == 0)
            return BadRequest(new AddCustomFeedErrorDto { Message = "At least one URL is required." });

        // Test that every URL is reachable and parseable BEFORE persisting.
        List<FeedUrlValidationResult> validation = await feedValidator.ValidateAsync(normalizedUrls, cancellationToken);
        Dictionary<string, string> urlErrors = validation
            .Where(r => !r.IsValid)
            .ToDictionary(r => r.Url, r => r.ErrorMessage ?? "Unknown error");

        if (urlErrors.Count > 0)
        {
            return BadRequest(new AddCustomFeedErrorDto
            {
                Message = $"{urlErrors.Count} of {normalizedUrls.Count} URL(s) could not be loaded.",
                UrlErrors = urlErrors,
            });
        }

        CustomFeedDescriptor created = await customFeedRepository.AddAsync(
            preferences.Id, normalizedUrls, dto.DisplayName.Trim(), dto.Language, cancellationToken);

        return Ok(new FeedDto
        {
            ClientName = CustomClientNamePrefix + created.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DisplayName = created.DisplayName,
            Language = created.Language,
            LanguageName = Languages.GetDisplayName(created.Language),
            Enabled = created.Enabled,
            IsCustom = true,
            CustomFeedId = created.Id,
            Urls = created.Urls,
            LastFetchErrorMessage = created.LastFetchErrorMessage,
        });
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
