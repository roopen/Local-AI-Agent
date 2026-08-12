using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.AI;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public sealed class AiSettingsStartupService(
    UserContext context,
    IAiSettingsSecretProtector secretProtector,
    ILlmRuntimeManager runtimeManager,
    ILoadLLMUseCase loadLlmUseCase,
    ILogger<AiSettingsStartupService> logger)
{
    public async Task UpgradePlaintextTokensAsync(CancellationToken cancellationToken = default)
    {
        List<AiSettings> settingsRows = await context.AiSettings.ToListAsync(cancellationToken);
        bool changed = false;

        foreach (AiSettings settings in settingsRows)
        {
            if (string.IsNullOrEmpty(settings.ApiKeyCiphertext)
                || secretProtector.IsProtected(settings.ApiKeyCiphertext))
            {
                continue;
            }

            settings.ApiKeyCiphertext = secretProtector.Protect(settings.ApiKeyCiphertext);
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ActivateFirstAndWarmUpAsync(CancellationToken cancellationToken = default)
    {
        AiSettings? settings = await context.AiSettings
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            logger.LogInformation("No database-backed LLM settings found; startup warm-up skipped");
            return false;
        }

        if (!secretProtector.TryUnprotect(settings.ApiKeyCiphertext, out string apiKey))
        {
            logger.LogWarning("The saved LLM API token cannot be decrypted; enter it again in settings");
            return false;
        }

        LlmRuntimeSnapshot candidate;
        try
        {
            candidate = runtimeManager.CreateCandidate(new LlmConnectionSettings(
                settings.ModelId,
                settings.EndpointUrl,
                apiKey,
                settings.Temperature,
                settings.TopP,
                settings.FrequencyPenalty,
                settings.PresencePenalty));
        }
        catch (LlmConnectionException ex)
        {
            logger.LogWarning("Saved LLM settings are invalid: {Message}", ex.Message);
            return false;
        }

        runtimeManager.Activate(candidate);
        return await loadLlmUseCase.LoadLLMUseCaseAsync(cancellationToken);
    }
}
