namespace LocalAIAgent.Application.News
{
    /// <summary>Result for a single URL's reachability test.</summary>
    public sealed record FeedUrlValidationResult(string Url, bool IsValid, string? ErrorMessage);

    /// <summary>
    /// Validates that a candidate RSS URL is reachable and parseable, before persisting it as a custom feed.
    /// Used by the Add-Feed flow to surface upstream errors immediately to the user.
    /// </summary>
    public interface IFeedValidator
    {
        Task<List<FeedUrlValidationResult>> ValidateAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);
    }
}
