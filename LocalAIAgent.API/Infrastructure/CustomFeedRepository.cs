using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.News;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class CustomFeedRepository(UserContext context) : ICustomFeedRepository
{
    public async Task<List<CustomFeedDescriptor>> GetForUserAsync(int userPreferencesId, CancellationToken cancellationToken = default)
    {
        return await context.CustomFeeds
            .Where(f => f.UserPreferencesId == userPreferencesId)
            .OrderBy(f => f.DisplayName)
            .Select(f => new CustomFeedDescriptor(f.Id, f.Urls, f.DisplayName, f.Language, f.Enabled, f.LastFetchErrorMessage))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomFeedDescriptor> AddAsync(int userPreferencesId, IEnumerable<string> urls, string displayName, string language, CancellationToken cancellationToken = default)
    {
        // Trim and dedupe; case-sensitive because RSS URLs typically have meaningful path casing.
        List<string> normalizedUrls = [.. urls
            .Select(u => u.Trim())
            .Where(u => u.Length > 0)
            .Distinct(StringComparer.Ordinal)];

        CustomFeed entity = new()
        {
            UserPreferencesId = userPreferencesId,
            Urls = normalizedUrls,
            DisplayName = displayName,
            Language = language,
            Enabled = true,
        };
        context.CustomFeeds.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return new CustomFeedDescriptor(entity.Id, entity.Urls, entity.DisplayName, entity.Language, entity.Enabled, entity.LastFetchErrorMessage);
    }

    public async Task<bool> RemoveAsync(int userPreferencesId, int customFeedId, CancellationToken cancellationToken = default)
    {
        CustomFeed? entity = await context.CustomFeeds
            .FirstOrDefaultAsync(f => f.Id == customFeedId && f.UserPreferencesId == userPreferencesId, cancellationToken);
        if (entity is null) return false;

        context.CustomFeeds.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetEnabledAsync(int userPreferencesId, int customFeedId, bool enabled, CancellationToken cancellationToken = default)
    {
        CustomFeed? entity = await context.CustomFeeds
            .FirstOrDefaultAsync(f => f.Id == customFeedId && f.UserPreferencesId == userPreferencesId, cancellationToken);
        if (entity is null) return false;

        entity.Enabled = enabled;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordFetchResultAsync(int customFeedId, string? errorMessage, CancellationToken cancellationToken = default)
    {
        CustomFeed? entity = await context.CustomFeeds.FirstOrDefaultAsync(f => f.Id == customFeedId, cancellationToken);
        if (entity is null) return;

        entity.LastFetchedAt = DateTime.UtcNow;
        entity.LastFetchErrorMessage = errorMessage;
        await context.SaveChangesAsync(cancellationToken);
    }
}
