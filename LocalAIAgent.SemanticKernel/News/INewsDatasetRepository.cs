using LocalAIAgent.Domain;

namespace LocalAIAgent.SemanticKernel.News
{
    public record CachedNewsEvaluation(Relevancy Relevancy, string? Topic, string? Reasoning, string? ModelUsed);

    public interface INewsDatasetRepository
    {
        Task SaveAsync(List<NewsArticle> articles, int userPreferencesId, bool useInDataset, string? modelUsed, CancellationToken cancellationToken);
        Task<Dictionary<string, CachedNewsEvaluation>> GetCachedEvaluationsAsync(IEnumerable<string> links, CancellationToken cancellationToken);
    }
}
