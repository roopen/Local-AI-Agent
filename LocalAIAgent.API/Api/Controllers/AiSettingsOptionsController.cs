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
public sealed class AiSettingsOptionsController(
    UserContext context,
    IAiSettingsSecretProtector secretProtector,
    ILlmRuntimeManager runtimeManager) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<AiSettingsCatalogResponse>> GetOptions(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out int userId))
            return Unauthorized();

        bool isOwner = User.IsInRole(AuthRoles.Owner);
        int? selectedSettingsId = await context.UserPreferences
            .AsNoTracking()
            .Where(preferences => preferences.UserId == userId)
            .Select(preferences => preferences.SelectedAiSettingsId)
            .FirstOrDefaultAsync(cancellationToken);

        List<AiSettings> settings = await context.AiSettings
            .AsNoTracking()
            .OrderBy(option => option.Id)
            .ToListAsync(cancellationToken);

        List<AiSettingsOptionResponse> options = settings
            .Where(option => isOwner || runtimeManager.IsConfiguredFor(option.Id))
            .Select(option => ToResponse(option, isOwner))
            .ToList();

        int? effectiveSelection = options.Any(option =>
            option.Id == selectedSettingsId && option.IsAvailable)
                ? selectedSettingsId
                : options.FirstOrDefault(option => option.IsAvailable)?.Id;

        return Ok(new AiSettingsCatalogResponse
        {
            IsConfigured = options.Any(option => option.IsAvailable),
            IsOwner = isOwner,
            SelectedSettingsId = effectiveSelection,
            Options = options,
        });
    }

    [HttpPost("options")]
    [Authorize(Roles = AuthRoles.Owner)]
    public Task<ActionResult<AiSettingsOptionResponse>> CreateOption(
        [FromBody] SaveAiSettingsOptionRequest request,
        CancellationToken cancellationToken) =>
        TestAndSaveAsync(null, request, cancellationToken);

    [HttpPut("options/{settingsId:int}")]
    [Authorize(Roles = AuthRoles.Owner)]
    public async Task<ActionResult<AiSettingsOptionResponse>> UpdateOption(
        int settingsId,
        [FromBody] SaveAiSettingsOptionRequest request,
        CancellationToken cancellationToken)
    {
        AiSettings? existing = await context.AiSettings
            .FirstOrDefaultAsync(option => option.Id == settingsId, cancellationToken);
        if (existing is null)
            return NotFound();

        return await TestAndSaveAsync(existing, request, cancellationToken);
    }

    [HttpDelete("options/{settingsId:int}")]
    [Authorize(Roles = AuthRoles.Owner)]
    public async Task<IActionResult> DeleteOption(
        int settingsId,
        CancellationToken cancellationToken)
    {
        AiSettings? existing = await context.AiSettings
            .FirstOrDefaultAsync(option => option.Id == settingsId, cancellationToken);
        if (existing is null)
            return NotFound();

        context.AiSettings.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        runtimeManager.Remove(settingsId);
        return NoContent();
    }

    [HttpPut("selection")]
    public async Task<IActionResult> SelectOption(
        [FromBody] SelectAiSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out int userId))
            return Unauthorized();

        if (!runtimeManager.IsConfiguredFor(request.SettingsId)
            || !await context.AiSettings.AsNoTracking()
                .AnyAsync(option => option.Id == request.SettingsId, cancellationToken))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "LLM option unavailable",
                Detail = "The selected LLM is not available.",
            });
        }

        UserPreferences? preferences = await context.UserPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preferences is null)
            return NotFound("User preferences not found.");

        preferences.SelectedAiSettingsId = request.SettingsId;
        await context.SaveChangesAsync(cancellationToken);
        runtimeManager.SetUserSelection(preferences.Id, request.SettingsId);
        return NoContent();
    }

    private async Task<ActionResult<AiSettingsOptionResponse>> TestAndSaveAsync(
        AiSettings? existing,
        SaveAiSettingsOptionRequest request,
        CancellationToken cancellationToken)
    {
        if (existing is not null && request.ConnectionSourceSettingsId is not null)
            return BadRequest("A saved LLM option cannot change hosts through connection reuse.");

        AiSettings? connectionSource = null;
        if (request.ConnectionSourceSettingsId is int sourceSettingsId)
        {
            connectionSource = await context.AiSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    option => option.Id == sourceSettingsId,
                    cancellationToken);
            if (connectionSource is null)
                return UnprocessableEntity(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Host unavailable",
                    Detail = "The host configuration selected for reuse no longer exists.",
                });
        }

        string apiKey;
        LlmRuntimeSnapshot? candidate = null;
        List<(AiSettings Settings, LlmRuntimeSnapshot Candidate)> sharedHostCandidates = [];
        try
        {
            apiKey = connectionSource is null
                ? ResolveApiKey(request, existing)
                : ResolveApiKey(connectionSource);
            string endpointUrl = connectionSource?.EndpointUrl
                ?? request.EndpointUrl
                ?? string.Empty;
            candidate = runtimeManager.CreateCandidate(new LlmConnectionSettings(
                request.ModelId,
                endpointUrl,
                apiKey,
                request.Temperature,
                request.TopP,
                request.FrequencyPenalty,
                request.PresencePenalty,
                request.UseResultsForDataset));
            await runtimeManager.WarmUpAsync(candidate, cancellationToken);

            if (existing is not null
                && HostConnectionChanged(existing, candidate.Options.EndpointUrl, request))
            {
                int hostId = GetHostId(existing);
                List<AiSettings> otherModels = await context.AiSettings
                    .Where(option => option.HostId == hostId && option.Id != existing.Id)
                    .ToListAsync(cancellationToken);
                foreach (AiSettings otherModel in otherModels)
                {
                    LlmRuntimeSnapshot otherCandidate = runtimeManager.CreateCandidate(
                        new LlmConnectionSettings(
                            otherModel.ModelId,
                            candidate.Options.EndpointUrl,
                            apiKey,
                            otherModel.Temperature,
                            otherModel.TopP,
                            otherModel.FrequencyPenalty,
                            otherModel.PresencePenalty,
                            otherModel.UseResultsForDataset));
                    sharedHostCandidates.Add((otherModel, otherCandidate));
                    await runtimeManager.WarmUpAsync(otherCandidate, cancellationToken);
                }
            }
        }
        catch (LlmConnectionException ex)
        {
            if (candidate is not null)
                runtimeManager.Discard(candidate);
            foreach ((_, LlmRuntimeSnapshot otherCandidate) in sharedHostCandidates)
                runtimeManager.Discard(otherCandidate);

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
            Name = request.Name.Trim(),
            ModelId = normalized.ModelId,
            EndpointUrl = normalized.EndpointUrl,
        };

        persisted.Name = request.Name.Trim();
        persisted.HostId = connectionSource is not null
            ? GetHostId(connectionSource)
            : existing is not null
                ? GetHostId(existing)
                : 0;
        persisted.ModelId = normalized.ModelId;
        persisted.EndpointUrl = normalized.EndpointUrl;
        string protectedApiKey = secretProtector.Protect(apiKey);
        persisted.ApiKeyCiphertext = protectedApiKey;
        persisted.Temperature = normalized.Temperature;
        persisted.TopP = normalized.TopP;
        persisted.FrequencyPenalty = normalized.FrequencyPenalty;
        persisted.PresencePenalty = normalized.PresencePenalty;
        persisted.UseResultsForDataset = normalized.UseResultsForDataset;

        if (existing is null)
            context.AiSettings.Add(persisted);

        foreach ((AiSettings otherModel, _) in sharedHostCandidates)
        {
            otherModel.EndpointUrl = normalized.EndpointUrl;
            otherModel.ApiKeyCiphertext = protectedApiKey;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (persisted.HostId == 0)
            {
                persisted.HostId = persisted.Id;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            runtimeManager.Discard(candidate);
            foreach ((_, LlmRuntimeSnapshot otherCandidate) in sharedHostCandidates)
                runtimeManager.Discard(otherCandidate);
            throw;
        }

        runtimeManager.Activate(persisted.Id, candidate);
        foreach ((AiSettings otherModel, LlmRuntimeSnapshot otherCandidate) in sharedHostCandidates)
            runtimeManager.Activate(otherModel.Id, otherCandidate);
        return Ok(ToResponse(persisted, includePrivateDetails: true));
    }

    private static bool HostConnectionChanged(
        AiSettings existing,
        string normalizedEndpointUrl,
        SaveAiSettingsOptionRequest request) =>
        !string.Equals(existing.EndpointUrl, normalizedEndpointUrl, StringComparison.Ordinal)
        || request.ClearApiKey
        || request.ApiKey is not null;

    private static int GetHostId(AiSettings settings) =>
        settings.HostId > 0 ? settings.HostId : settings.Id;

    private string ResolveApiKey(
        SaveAiSettingsOptionRequest request,
        AiSettings? existing)
    {
        if (request.ClearApiKey)
            return string.Empty;

        if (request.ApiKey is not null)
            return request.ApiKey;

        if (existing is null || string.IsNullOrEmpty(existing.ApiKeyCiphertext))
            return string.Empty;

        if (!secretProtector.TryUnprotect(existing.ApiKeyCiphertext, out string apiKey))
        {
            throw new LlmConnectionException(
                "The saved API token can no longer be decrypted. Enter it again.");
        }

        return apiKey;
    }

    private string ResolveApiKey(AiSettings connectionSource)
    {
        if (string.IsNullOrEmpty(connectionSource.ApiKeyCiphertext))
            return string.Empty;

        if (!secretProtector.TryUnprotect(connectionSource.ApiKeyCiphertext, out string apiKey))
        {
            throw new LlmConnectionException(
                "The saved API token for the selected host can no longer be decrypted. Enter it again on that LLM option.");
        }

        return apiKey;
    }

    private AiSettingsOptionResponse ToResponse(
        AiSettings settings,
        bool includePrivateDetails)
    {
        bool canDecrypt = secretProtector.TryUnprotect(
            settings.ApiKeyCiphertext,
            out string apiKey);

        return new AiSettingsOptionResponse
        {
            Id = settings.Id,
            HostId = GetHostId(settings),
            Name = settings.Name,
            ModelId = settings.ModelId,
            EndpointUrl = includePrivateDetails ? settings.EndpointUrl : string.Empty,
            HasApiKey = includePrivateDetails && canDecrypt && !string.IsNullOrEmpty(apiKey),
            IsAvailable = canDecrypt && runtimeManager.IsConfiguredFor(settings.Id),
            UseResultsForDataset = includePrivateDetails && settings.UseResultsForDataset,
            Temperature = includePrivateDetails ? settings.Temperature : 0,
            TopP = includePrivateDetails ? settings.TopP : 0,
            FrequencyPenalty = includePrivateDetails ? settings.FrequencyPenalty : 0,
            PresencePenalty = includePrivateDetails ? settings.PresencePenalty : 0,
        };
    }
}
