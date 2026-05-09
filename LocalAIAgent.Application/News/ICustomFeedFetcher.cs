namespace LocalAIAgent.Application.News
{
    /// <summary>
    /// Fetches a user's custom RSS feeds and converts entries into <see cref="NewsItem"/>.
    /// Each item is tagged with <c>SourceClientName = "custom:{id}"</c> and the feed's declared language.
    /// </summary>
    public interface ICustomFeedFetcher
    {
        Task<List<NewsItem>> FetchAsync(IEnumerable<CustomFeedDescriptor> enabledFeeds, CancellationToken cancellationToken = default);
    }
}
