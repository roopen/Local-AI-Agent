using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class NewsDatasetRepository(UserContext context) : INewsDatasetRepository
{
    public async Task<Dictionary<string, CachedNewsEvaluation>> GetCachedEvaluationsAsync(
        IEnumerable<string> links,
        CancellationToken cancellationToken)
    {
        List<string> linkList = links.ToList();
        return await context.NewsEvaluationEntries
            .Where(e => linkList.Contains(e.ArticleLink))
            .ToDictionaryAsync(
                e => e.ArticleLink,
                e => new CachedNewsEvaluation(
                    Enum.TryParse<Relevancy>(e.Relevancy, out Relevancy r) ? r : Relevancy.Low,
                    e.ArticleTopic,
                    e.Reasoning,
                    e.ModelUsed),
                cancellationToken);
    }

    public async Task SaveAsync(List<NewsArticle> articles, int userPreferencesId, bool useInDataset, string? modelUsed, CancellationToken cancellationToken)
    {
        HashSet<string> existingLinks = await context.NewsEvaluationEntries
            .Where(e => articles.Select(a => a.Link).Contains(e.ArticleLink))
            .Select(e => e.ArticleLink)
            .ToHashSetAsync(cancellationToken);

        foreach (NewsArticle article in articles)
        {
            if (existingLinks.Contains(article.Link))
                continue;

            context.NewsEvaluationEntries.Add(new NewsEvaluationEntry
            {
                ArticleTitle = article.Title,
                ArticleSummary = article.Summary,
                ArticleLink = article.Link,
                ArticleSource = article.Source,
                ArticleTopic = article.Topic,
                Relevancy = article.Relevancy.ToString(),
                Reasoning = article.Reasoning,
                UseInDataset = useInDataset,
                ModelUsed = modelUsed ?? "Unknown",
                UserPreferencesId = userPreferencesId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
