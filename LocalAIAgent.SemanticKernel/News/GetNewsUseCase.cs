using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News.AI;

namespace LocalAIAgent.SemanticKernel.News
{
    public interface IGetNewsUseCase
    {
        Task<List<NewsItem>> GetAsync(UserPreferences preferences);
        Task<EvaluatedNewsArticles> GetAsyncV2(UserPreferences preferences);
    }

    public class GetNewsUseCase(
        INewsService newsService,
        IEvaluateNewsUseCase evaluateNewsUseCase,
        IGetTranslationUseCase getTranslationUseCase) : IGetNewsUseCase
    {
        public async Task<List<NewsItem>> GetAsync(UserPreferences preferences)
        {
            List<NewsItem> newsItems = await newsService.GetNewsAsync(preferences.Dislikes);

            return await evaluateNewsUseCase.EvaluateArticles(newsItems, preferences);
        }

        public async Task<EvaluatedNewsArticles> GetAsyncV2(UserPreferences preferences)
        {
            List<NewsItem> newsItems = await newsService.GetNewsAsync(preferences.Dislikes);

            EvaluatedNewsArticles evaluatedArticles = await evaluateNewsUseCase.EvaluateArticlesV2(newsItems, preferences);

            evaluatedArticles.NewsArticles = await getTranslationUseCase.TranslateArticleAsync(evaluatedArticles.NewsArticles, "English");

            return evaluatedArticles;
        }
    }
}
