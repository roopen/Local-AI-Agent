using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News.AI;
using System.Runtime.CompilerServices;

namespace LocalAIAgent.SemanticKernel.News
{
    public interface IGetNewsUseCase
    {
        IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(UserPreferences preferences, CancellationToken cancellationToken);
    }

    public class GetNewsUseCase(
        INewsService newsService,
        IEvaluateNewsUseCase evaluateNewsUseCase,
        IGetTranslationUseCase getTranslationUseCase) : IGetNewsUseCase
    {
        public async IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(
            UserPreferences preferences,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            List<NewsItem> newsItems = await newsService.GetNewsAsync(preferences.Dislikes);

            foreach (NewsItem[] newsBatch in newsItems.Chunk(5))
            {
                EvaluatedNewsArticles evaluatedArticles = await evaluateNewsUseCase.EvaluateArticlesV2(newsBatch.ToList(), preferences);
                List<NewsArticle> newsArticles = await getTranslationUseCase.TranslateArticleAsync(evaluatedArticles.NewsArticles, "English");

                foreach (NewsArticle article in newsArticles)
                {
                    yield return article;
                }
            }
        }
    }
}
