using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News;

namespace LocalAIAgent.SemanticKernel
{
    public interface IGetNewsUseCase
    {
        Task<List<NewsItem>> GetAsync(UserPreferences? preferences);
    }

    public class GetNewsUseCase(
        INewsService newsService,
        IEvaluateNewsUseCase evaluateNewsUseCase) : IGetNewsUseCase
    {
        public async Task<List<NewsItem>> GetAsync(UserPreferences? preferences)
        {
            List<NewsItem> newsItems = await newsService.GetNewsAsync();

            if (preferences == null)
            {
                return newsItems;
            }

            return await evaluateNewsUseCase.EvaluateArticles(newsItems, preferences);
        }
    }
}
