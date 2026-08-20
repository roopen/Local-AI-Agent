using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.Chat;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public sealed class AiSettingsStartupService(
    UserContext context,
    IAiSettingsSecretProtector secretProtector,
    ILlmRuntimeManager runtimeManager,
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

    public async Task<bool> ActivateFirstAsync(CancellationToken cancellationToken = default)
    {
        AiSettings? settings = await context.AiSettings
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            logger.LogInformation("No database-backed LLM settings found; startup activation skipped");
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

        runtimeManager.Activate(settings.Id, candidate);
        return true;
    }

    public async Task<int> ActivateAllAsync(CancellationToken cancellationToken = default)
    {
        List<AiSettings> settingsRows = await context.AiSettings
            .AsNoTracking()
            .OrderBy(settings => settings.Id)
            .ToListAsync(cancellationToken);

        int activatedCount = 0;
        foreach (AiSettings settings in settingsRows)
        {
            if (!secretProtector.TryUnprotect(settings.ApiKeyCiphertext, out string apiKey))
            {
                logger.LogWarning(
                    "The saved API token for LLM option {SettingsId} cannot be decrypted",
                    settings.Id);
                continue;
            }

            try
            {
                LlmRuntimeSnapshot candidate = runtimeManager.CreateCandidate(
                    new LlmConnectionSettings(
                        settings.ModelId,
                        settings.EndpointUrl,
                        apiKey,
                        settings.Temperature,
                        settings.TopP,
                        settings.FrequencyPenalty,
                        settings.PresencePenalty));
                runtimeManager.Activate(settings.Id, candidate);
                activatedCount++;
            }
            catch (LlmConnectionException ex)
            {
                logger.LogWarning(
                    "Saved LLM option {SettingsId} is invalid: {Message}",
                    settings.Id,
                    ex.Message);
            }
        }

        if (activatedCount == 0)
        {
            logger.LogInformation(
                "No usable database-backed LLM settings found; startup activation skipped");
            return 0;
        }

        List<(int PreferencesId, int SettingsId)> selections = await context.UserPreferences
            .AsNoTracking()
            .Where(preferences => preferences.SelectedAiSettingsId != null)
            .Select(preferences => new ValueTuple<int, int>(
                preferences.Id,
                preferences.SelectedAiSettingsId!.Value))
            .ToListAsync(cancellationToken);

        foreach ((int preferencesId, int settingsId) in selections)
        {
            if (runtimeManager.IsConfiguredFor(settingsId))
                runtimeManager.SetUserSelection(preferencesId, settingsId);
        }

        return activatedCount;
    }
}
