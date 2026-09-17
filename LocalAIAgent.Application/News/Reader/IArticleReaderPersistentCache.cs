namespace LocalAIAgent.Application.News.Reader;

public sealed record ArticleReaderCacheKey(int UserId, string Url, string TargetLanguage, string Endpoint, string Model);
public sealed record CachedArticle(ReadArticleResult Result, DateTimeOffset ExpiresAt);

public interface IArticleReaderPersistentCache
{
    Task<CachedArticle?> GetAsync(ArticleReaderCacheKey key, CancellationToken cancellationToken);
    Task SetAsync(ArticleReaderCacheKey key, CachedArticle article, CancellationToken cancellationToken);
}
