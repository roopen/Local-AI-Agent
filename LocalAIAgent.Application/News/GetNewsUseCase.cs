using LocalAIAgent.Domain;
using LocalAIAgent.Application.Chat;
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
        IEvaluateNewsUseCase evaluateNewsUseCase,
        IGetTranslationUseCase getTranslationUseCase) : IGetNewsUseCase
    {
        public async IAsyncEnumerable<NewsArticle> GetNewsStreamAsync(
            UserPreferences preferences,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            List<NewsItem> newsItems = await newsService.GetNewsAsync(preferences);

#if DEBUG
            bool saveDataset = true;
#else
            bool saveDataset = false;
#endif
            foreach (NewsItem[] newsBatch in newsItems.Chunk(5))
            {
                EvaluatedNewsArticles evaluatedArticles = await evaluateNewsUseCase.EvaluateArticlesV2(
                    newsBatch.ToList(),
                    preferences,
                    includeReasoning: saveDataset);

                evaluatedArticles.NewsArticles = evaluatedArticles.NewsArticles.Where(a => a.Relevancy is Relevancy.High).ToList();
                string targetLanguage = string.IsNullOrEmpty(preferences.TargetLanguage) ? "en" : preferences.TargetLanguage;
                List<NewsArticle> newsArticles = await getTranslationUseCase.TranslateArticleAsync(evaluatedArticles.NewsArticles, targetLanguage);

                foreach (NewsArticle article in newsArticles)
                {
                    yield return article;
                }
            }
        }
    }
}
