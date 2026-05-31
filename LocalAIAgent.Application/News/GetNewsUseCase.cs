using LocalAIAgent.Domain;
using LocalAIAgent.Application.News.AI;
using System.Runtime.CompilerServices;

namespace LocalAIAgent.Application.News
{
    public interface IGetNewsUseCase
    {
        IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(UserPreferences preferences, CancellationToken cancellationToken);
    }

    public class GetNewsUseCase(
        INewsService newsService,
        ICustomFeedRepository customFeedRepository,
        ICustomFeedFetcher customFeedFetcher,
        IEvaluateNewsUseCase evaluateNewsUseCase,
        IGetTranslationUseCase getTranslationUseCase) : IGetNewsUseCase
    {
        public async IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(
            UserPreferences preferences,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            List<NewsItem> builtInItems = await newsService.GetNewsAsync(preferences);

            // Fetch the user's enabled custom feeds and merge into the stream.
            List<CustomFeedDescriptor> customFeeds = await customFeedRepository.GetForUserAsync(preferences.Id, cancellationToken);
            List<CustomFeedDescriptor> enabledCustom = [.. customFeeds.Where(f => f.Enabled)];
            List<NewsItem> customItems = enabledCustom.Count == 0
                ? []
                : await customFeedFetcher.FetchAsync(enabledCustom, cancellationToken);

            // Merge and dedupe by Link so a user's custom feed pointing at a built-in URL doesn't duplicate.
            List<NewsItem> newsItems = [.. builtInItems.Concat(customItems).DistinctBy(i => i.Link)];

#if DEBUG
            bool saveDataset = true;
#else
            bool saveDataset = false;
#endif
            foreach (NewsItem[] newsBatch in newsItems.Chunk(5))
            {
                FeedKeywordEvaluationResult keywordEvaluation = newsService.EvaluateFeedKeywords(
                    newsBatch,
                    preferences,
                    includeReasoning: saveDataset);

                List<NewsArticle> evaluatedNewsArticles = [.. keywordEvaluation.EvaluatedArticles];
                if (keywordEvaluation.UnresolvedArticles.Length > 0)
                {
                    EvaluatedNewsArticles llmEvaluatedArticles = await evaluateNewsUseCase.EvaluateArticlesV2(
                        keywordEvaluation.UnresolvedArticles.ToList(),
                        preferences,
                        includeReasoning: saveDataset);
                    evaluatedNewsArticles.AddRange(llmEvaluatedArticles.NewsArticles);
                }

                evaluatedNewsArticles = [.. evaluatedNewsArticles.Where(a => a.Relevancy is Relevancy.High)];
                string targetLanguage = string.IsNullOrEmpty(preferences.TargetLanguage) ? "en" : preferences.TargetLanguage;
                List<NewsArticle> newsArticles = await getTranslationUseCase.TranslateArticleAsync(evaluatedNewsArticles, targetLanguage);

                foreach (NewsArticle article in newsArticles)
                {
                    yield return article;
                }
            }
        }
    }
}
