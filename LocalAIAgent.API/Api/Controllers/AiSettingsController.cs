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
    ILlmRuntimeManager runtimeManager,
    IConfiguration configuration) : ControllerBase
{
    private const string DefaultModelId = "gemma-3-27b-it-qat";
    private const string DefaultEndpointUrl = "http://localhost:1234/v1/";

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();

        AiSettings? settings = await context.AiSettings
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        AiSettingsResponse response = settings is null ? CreateDefaults() : ToResponse(settings);
        if (!User.IsInRole(AuthRoles.Owner))
        {
            response = response with
            {
                HasApiKey = false,
                ModelId = string.Empty,
                EndpointUrl = string.Empty,
                Temperature = 0,
                TopP = 0,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
            };
        }

        return Ok(response);
    }

    [HttpPut]
    [Authorize(Roles = AuthRoles.Owner)]
    public async Task<ActionResult<AiSettingsResponse>> Put(
        [FromBody] UpdateAiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out _))
            return Unauthorized();

        AiSettings? existing = await context.AiSettings
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

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

    private AiSettingsResponse CreateDefaults() => new()
    {
        IsConfigured = false,
        HasApiKey = false,
        ModelId = configuration["AI_DEFAULT_MODEL"] ?? DefaultModelId,
        EndpointUrl = configuration["AI_DEFAULT_ENDPOINT"] ?? DefaultEndpointUrl,
        Temperature = 0.2m,
        TopP = 1m,
        FrequencyPenalty = 1m,
        PresencePenalty = 1m,
    };

}
