using LocalAIAgent.Domain;

namespace LocalAIAgent.SemanticKernel.News
{
    public record CachedTranslation(string Title, string Summary);

    public interface IArticleTranslationRepository
    {
        Task<Dictionary<string, CachedTranslation>> GetCachedTranslationsAsync(IEnumerable<string> links, string targetLanguage, CancellationToken cancellationToken = default);
        Task SaveTranslationsAsync(List<NewsArticle> articles, List<(string OriginalTitle, string OriginalSummary)> originals, string targetLanguage, CancellationToken cancellationToken = default);
    }
}
