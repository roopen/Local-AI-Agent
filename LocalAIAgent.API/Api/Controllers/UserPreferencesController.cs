using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserPreferencesController(UserContext context) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserPreferenceDto>> GetPreferences(int userId)
    {
        UserPreferences? preferences = await context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences == null)
        {
            return NotFound();
        }
        return Ok(new UserPreferenceDto
        {
            Id = preferences.Id,
            Prompt = preferences.Prompt,
            Interests = preferences.Interests,
            Dislikes = preferences.Dislikes
        });
    }

    [HttpPost("/api/SavePreferences")]
    public async Task<IActionResult> SavePreferences([FromBody] UserPreferenceDto preferences)
    {
        User? user = await context.Users.Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == preferences.UserId);
        if (user == null)
        {
            return NotFound();
        }

        if (user.Preferences == null)
        {
            user.Preferences = new UserPreferences
            {
                Prompt = preferences.Prompt,
                Interests = preferences.Interests,
                Dislikes = preferences.Dislikes
            };
            context.UserPreferences.Add(user.Preferences);
        }
        else
        {
            user.Preferences.Prompt = preferences.Prompt;
            user.Preferences.Interests = preferences.Interests;
            user.Preferences.Dislikes = preferences.Dislikes;
        }

        await context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("api/GetAiSettings")]
    public async Task<ActionResult<AiSettingsDto>> GetAiSettings([FromBody] int userId)
    {
        AiSettings? aiSettings = await context.AiSettings
            .Include(a => a.UserPreferences)
            .FirstOrDefaultAsync(a => a.UserPreferences.UserId == userId);

        if (aiSettings == null) return NotFound();

        return Ok(new AiSettingsDto
        {
            ModelId = aiSettings.ModelId,
            ApiKey = aiSettings.ApiKey,
            EndpointUrl = aiSettings.EndpointUrl,
            Temperature = aiSettings.Temperature,
            TopP = aiSettings.TopP,
            FrequencyPenalty = aiSettings.FrequencyPenalty,
            PresencePenalty = aiSettings.PresencePenalty
        });
    }

    [HttpPost("/api/SaveAiSettings")]
    public async Task<IActionResult> SaveAiSettings([FromBody] AiSettingsDto aiSettings)
    {
        User? user = await context.Users.Include(u => u.Preferences).FirstOrDefaultAsync();
        if (user is null) return NotFound();

        if (user.Preferences is null)
            return BadRequest("User preferences not found");

        var aiSettingsExisting = context.AiSettings.FirstOrDefault(a => a.UserPreferencesId == user.Preferences.Id);

        if (aiSettingsExisting is null)
        {
            var newAiSettings = new AiSettings
            {
                ModelId = aiSettings.ModelId,
                Temperature = aiSettings.Temperature,
                EndpointUrl = aiSettings.EndpointUrl,
                ApiKey = aiSettings.ApiKey,
                UserPreferencesId = user.Preferences.Id,
                UserPreferences = user.Preferences,
                FrequencyPenalty = aiSettings.FrequencyPenalty,
                PresencePenalty = aiSettings.PresencePenalty
            };
            context.AiSettings.Add(newAiSettings);
        }
        else
        {
            aiSettingsExisting.ModelId = aiSettings.ModelId;
            aiSettingsExisting.Temperature = aiSettings.Temperature;
            aiSettingsExisting.EndpointUrl = aiSettings.EndpointUrl;
            aiSettingsExisting.ApiKey = aiSettings.ApiKey;
            aiSettingsExisting.TopP = aiSettings.TopP;
            aiSettingsExisting.FrequencyPenalty = aiSettings.FrequencyPenalty;
            aiSettingsExisting.PresencePenalty = aiSettings.PresencePenalty;
        }

        await context.SaveChangesAsync();

        return Ok();
    }
}