using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Infrastructure;

public class ArticleTranslationRepository(UserContext context) : IArticleTranslationRepository
{
    public async Task<Dictionary<string, CachedTranslation>> GetCachedTranslationsAsync(
        IEnumerable<string> links,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        List<string> linkList = links.ToList();
        return await context.ArticleTranslations
            .Where(t => t.TargetLanguage == targetLanguage && linkList.Contains(t.ArticleLink))
            .ToDictionaryAsync(
                t => t.ArticleLink,
                t => new CachedTranslation(t.TranslatedTitle, t.TranslatedSummary),
                cancellationToken);
    }

    public async Task SaveTranslationsAsync(List<NewsArticle> articles, List<(string OriginalTitle, string OriginalSummary)> originals, string targetLanguage, CancellationToken cancellationToken = default)
    {
        HashSet<string> existingLinks = await context.ArticleTranslations
            .Where(t => articles.Select(a => a.Link).Contains(t.ArticleLink))
            .Select(t => t.ArticleLink)
            .ToHashSetAsync(cancellationToken);

        for (int i = 0; i < articles.Count; i++)
        {
            NewsArticle article = articles[i];
            if (existingLinks.Contains(article.Link))
                continue;

            context.ArticleTranslations.Add(new ArticleTranslation
            {
                ArticleLink = article.Link,
                OriginalTitle = originals[i].OriginalTitle,
                OriginalSummary = originals[i].OriginalSummary,
                TranslatedTitle = article.Title,
                TranslatedSummary = article.Summary,
                TargetLanguage = targetLanguage,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
