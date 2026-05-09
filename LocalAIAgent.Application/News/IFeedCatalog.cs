namespace LocalAIAgent.Application.News
{
    /// <summary>Public projection of a built-in news source for use across project boundaries.</summary>
    public sealed record FeedDescriptor(string ClientName, string DisplayName, string Language);

    /// <summary>Read-only registry of the built-in news sources known at compile time.</summary>
    public interface IFeedCatalog
    {
        IReadOnlyList<FeedDescriptor> GetAllFeeds();
    }

    internal sealed class FeedCatalog(IEnumerable<BaseNewsClientSettings> sources) : IFeedCatalog
    {
        public IReadOnlyList<FeedDescriptor> GetAllFeeds() =>
            [.. sources
                .Select(s => new FeedDescriptor(s.ClientName, s.DisplayName, s.Language))
                .OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }
}
