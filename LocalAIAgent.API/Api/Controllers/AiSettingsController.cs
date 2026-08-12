using System.Globalization;
using System.Security.Claims;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-settings")]
public sealed class AiSettingsController(
    UserContext context,
    IAiSettingsSecretProtector secretProtector,
    ILlmRuntimeManager runtimeManager) : ControllerBase
{
    private const string DefaultModelId = "gemma-3-27b-it-qat";
    private const string DefaultEndpointUrl = "http://localhost:1234/v1/";

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out int userId))
            return Unauthorized();

        AiSettings? settings = await context.AiSettings
            .AsNoTracking()
            .Include(a => a.UserPreferences)
            .FirstOrDefaultAsync(a => a.UserPreferences.UserId == userId, cancellationToken);

        return Ok(settings is null ? CreateDefaults() : ToResponse(settings));
    }

    [HttpPut]
    public async Task<ActionResult<AiSettingsResponse>> Put(
        [FromBody] UpdateAiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out int userId))
            return Unauthorized();

        User? user = await context.Users
            .Include(u => u.Preferences)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return NotFound();
        if (user.Preferences is null)
            return BadRequest("User preferences must be created before LLM settings.");

        AiSettings? existing = await context.AiSettings
            .FirstOrDefaultAsync(a => a.UserPreferencesId == user.Preferences.Id, cancellationToken);

        string apiKey;
        LlmRuntimeSnapshot? candidate = null;
        try
        {
            apiKey = ResolveApiKey(request, existing);
            candidate = runtimeManager.CreateCandidate(new LlmConnectionSettings(
                request.ModelId,
                request.EndpointUrl,
                apiKey,
                request.Temperature,
                request.TopP,
                request.FrequencyPenalty,
                request.PresencePenalty));
            await runtimeManager.WarmUpAsync(candidate, cancellationToken);
        }
        catch (LlmConnectionException ex)
        {
            if (candidate is not null)
                runtimeManager.Discard(candidate);

            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Unable to connect to the AI service",
                Detail = ex.Message,
            });
        }

        AIOptions normalized = candidate.Options;
        AiSettings persisted = existing ?? new AiSettings
        {
            UserPreferencesId = user.Preferences.Id,
            UserPreferences = user.Preferences,
            ModelId = normalized.ModelId,
            EndpointUrl = normalized.EndpointUrl,
        };

        persisted.ModelId = normalized.ModelId;
        persisted.EndpointUrl = normalized.EndpointUrl;
        persisted.ApiKeyCiphertext = secretProtector.Protect(apiKey);
        persisted.Temperature = normalized.Temperature;
        persisted.TopP = normalized.TopP;
        persisted.FrequencyPenalty = normalized.FrequencyPenalty;
        persisted.PresencePenalty = normalized.PresencePenalty;

        if (existing is null)
            context.AiSettings.Add(persisted);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            runtimeManager.Discard(candidate);
            throw;
        }

        runtimeManager.Activate(candidate);
        return Ok(ToResponse(persisted));
    }

    private string ResolveApiKey(UpdateAiSettingsRequest request, AiSettings? existing)
    {
        if (request.ClearApiKey)
            return string.Empty;

        if (request.ApiKey is not null)
            return request.ApiKey;

        if (existing is null || string.IsNullOrEmpty(existing.ApiKeyCiphertext))
            return string.Empty;

        if (!secretProtector.TryUnprotect(existing.ApiKeyCiphertext, out string apiKey))
            throw new LlmConnectionException("The saved API token can no longer be decrypted. Enter it again.");

        return apiKey;
    }

    private AiSettingsResponse ToResponse(AiSettings settings)
    {
        bool canDecrypt = secretProtector.TryUnprotect(settings.ApiKeyCiphertext, out string apiKey);
        bool hasApiKey = canDecrypt && !string.IsNullOrEmpty(apiKey);

        return new AiSettingsResponse
        {
            IsConfigured = canDecrypt,
            HasApiKey = hasApiKey,
            ModelId = settings.ModelId,
            EndpointUrl = settings.EndpointUrl,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            FrequencyPenalty = settings.FrequencyPenalty,
            PresencePenalty = settings.PresencePenalty,
        };
    }

    private static AiSettingsResponse CreateDefaults() => new()
    {
        IsConfigured = false,
        HasApiKey = false,
        ModelId = DefaultModelId,
        EndpointUrl = DefaultEndpointUrl,
        Temperature = 0.2m,
        TopP = 1m,
        FrequencyPenalty = 1m,
        PresencePenalty = 1m,
    };

    private bool TryGetCurrentUserId(out int userId)
    {
        string? claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out userId);
    }
}
