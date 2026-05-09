namespace LocalAIAgent.Application.News
{
    /// <summary>Read projection of a custom feed; doesn't leak the EF entity across layers.</summary>
    public sealed record CustomFeedDescriptor(int Id, string Url, string DisplayName, string Language, bool Enabled, string? LastFetchErrorMessage);

    /// <summary>CRUD for user-owned RSS feeds.</summary>
    public interface ICustomFeedRepository
    {
        Task<List<CustomFeedDescriptor>> GetForUserAsync(int userPreferencesId, CancellationToken cancellationToken = default);

        Task<CustomFeedDescriptor> AddAsync(int userPreferencesId, string url, string displayName, string language, CancellationToken cancellationToken = default);

        Task<bool> RemoveAsync(int userPreferencesId, int customFeedId, CancellationToken cancellationToken = default);

        Task<bool> SetEnabledAsync(int userPreferencesId, int customFeedId, bool enabled, CancellationToken cancellationToken = default);

        Task RecordFetchResultAsync(int customFeedId, string? errorMessage, CancellationToken cancellationToken = default);
    }
}
