using LocalAIAgent.Domain;
using LocalAIAgent.Application.News.AI;
using System.Runtime.CompilerServices;

namespace LocalAIAgent.Application.News
{
    public enum NewsLoadingPhase
    {
        Feeds,
        Llm,
    }

    public interface IGetNewsUseCase
    {
        IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(
            UserPreferences preferences,
            CancellationToken cancellationToken,
            Func<NewsLoadingPhase, CancellationToken, Task>? loadingPhaseChanged = null);
    }

    public class GetNewsUseCase(
        INewsService newsService,
        ICustomFeedRepository customFeedRepository,
        ICustomFeedFetcher customFeedFetcher,
        ILoadLLMUseCase loadLlmUseCase,
        IEvaluateNewsUseCase evaluateNewsUseCase,
        IGetTranslationUseCase getTranslationUseCase) : IGetNewsUseCase
    {
        public async IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(
            UserPreferences preferences,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            Func<NewsLoadingPhase, CancellationToken, Task>? loadingPhaseChanged = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Start the lightweight `hi`/one-token warm-up before feed I/O so model loading
            // overlaps the slowest network-bound part of the news pipeline.
            Task<bool> llmWarmupTask = loadLlmUseCase.LoadLLMUseCaseAsync(cancellationToken);
            List<NewsItem> builtInItems = await newsService.GetNewsAsync(preferences, cancellationToken);

            // Fetch the user's enabled custom feeds and merge into the stream.
            List<CustomFeedDescriptor> customFeeds = await customFeedRepository.GetForUserAsync(preferences.Id, cancellationToken);
            List<CustomFeedDescriptor> enabledCustom = [.. customFeeds.Where(f => f.Enabled)];
            List<NewsItem> customItems = enabledCustom.Count == 0
                ? []
                : await customFeedFetcher.FetchAsync(enabledCustom, cancellationToken);

            // Merge and dedupe by Link so a user's custom feed pointing at a built-in URL doesn't duplicate.
            List<NewsItem> newsItems = [.. builtInItems.Concat(customItems).DistinctBy(i => i.Link)];

            // Do not let a real evaluation race the warm-up request if feed loading wins.
            await llmWarmupTask;

#if DEBUG
            bool saveDataset = true;
#else
            bool saveDataset = false;
#endif
            bool llmPhaseReported = false;
            foreach (NewsItem[] newsBatch in newsItems.Chunk(5))
            {
                cancellationToken.ThrowIfCancellationRequested();
                FeedKeywordEvaluationResult keywordEvaluation = newsService.EvaluateFeedKeywords(
                    newsBatch,
                    preferences,
                    includeReasoning: saveDataset);

                List<NewsArticle> evaluatedNewsArticles = [.. keywordEvaluation.EvaluatedArticles];
                if (keywordEvaluation.UnresolvedArticles.Length > 0)
                {
                    if (!llmPhaseReported && loadingPhaseChanged is not null)
                    {
                        await loadingPhaseChanged(NewsLoadingPhase.Llm, cancellationToken);
                        llmPhaseReported = true;
                    }

                    EvaluatedNewsArticles llmEvaluatedArticles = await evaluateNewsUseCase.EvaluateArticlesV2(
                        keywordEvaluation.UnresolvedArticles.ToList(),
                        preferences,
                        includeReasoning: saveDataset,
                        cancellationToken);
                    evaluatedNewsArticles.AddRange(llmEvaluatedArticles.NewsArticles);
                }

                evaluatedNewsArticles = [.. evaluatedNewsArticles.Where(a => a.Relevancy is Relevancy.High)];
                string targetLanguage = string.IsNullOrEmpty(preferences.TargetLanguage) ? "en" : preferences.TargetLanguage;
                List<NewsArticle> newsArticles = await getTranslationUseCase.TranslateArticleAsync(
                    evaluatedNewsArticles,
                    targetLanguage,
                    cancellationToken);

                foreach (NewsArticle article in newsArticles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return article;
                }
            }
        }
    }
}
