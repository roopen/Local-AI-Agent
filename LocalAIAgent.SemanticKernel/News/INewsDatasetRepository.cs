using LocalAIAgent.Domain;

namespace LocalAIAgent.SemanticKernel.News
{
    public record CachedNewsEvaluation(Relevancy Relevancy, string? Topic, string? Reasoning);

    public interface INewsDatasetRepository
    {
        Task SaveAsync(List<NewsArticle> articles, int userPreferencesId, bool useInDataset, CancellationToken cancellationToken);
        Task<Dictionary<string, CachedNewsEvaluation>> GetCachedEvaluationsAsync(IEnumerable<string> links, CancellationToken cancellationToken);
    }
}
