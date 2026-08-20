using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;
using Moq;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.Tests.UseCaseTests;

public class GetNewsUseCaseTests
{
    private static readonly UserPreferences TestPrefs = new()
    {
        Id = 7,
        Prompt = "Be helpful.",
        Interests = ["AI"],
        Dislikes = ["Crypto"],
        TargetLanguage = "en",
    };

    private static NewsItem MakeItem(string title, string summary, string link)
    {
        SyndicationItem item = new()
        {
            Title = new TextSyndicationContent(title),
            Summary = new TextSyndicationContent(summary),
            PublishDate = DateTimeOffset.UtcNow,
        };
        item.Links.Add(new SyndicationLink(new Uri(link)));
        return new NewsItem(item);
    }

    [Fact]
    public async Task GetNewsStreamAsync_DislikedKeywordArticles_AreNotSentToLlmEvaluator()
    {
        NewsItem dislikedItem = MakeItem("Crypto tagged by RSS", "summary", "https://x.com/disliked");
        NewsItem unresolvedItem = MakeItem("Needs model", "summary", "https://x.com/unresolved");

        NewsArticle dislikedArticle = new()
        {
            Title = dislikedItem.Title,
            Summary = dislikedItem.Summary,
            PublishedDate = dislikedItem.PublishDate.DateTime,
            Link = dislikedItem.Link!,
            Source = dislikedItem.Source ?? string.Empty,
            SourceLanguage = dislikedItem.Language,
            Categories = [],
            Relevancy = Relevancy.Low,
            Topic = null,
        };
        NewsArticle llmArticle = new()
        {
            Title = unresolvedItem.Title,
            Summary = unresolvedItem.Summary,
            PublishedDate = unresolvedItem.PublishDate.DateTime,
            Link = unresolvedItem.Link!,
            Source = unresolvedItem.Source ?? string.Empty,
            SourceLanguage = unresolvedItem.Language,
            Categories = [],
            Relevancy = Relevancy.High,
            Topic = "Tech",
        };

        Mock<INewsService> newsService = new(MockBehavior.Strict);
        newsService.Setup(s => s.GetNewsAsync(TestPrefs))
            .ReturnsAsync([dislikedItem, unresolvedItem]);
        newsService.Setup(s => s.EvaluateFeedKeywords(
                It.Is<NewsItem[]>(items => items.SequenceEqual(new[] { dislikedItem, unresolvedItem })),
                TestPrefs,
                It.IsAny<bool>()))
            .Returns(new FeedKeywordEvaluationResult([dislikedArticle], [unresolvedItem]));

        Mock<ICustomFeedRepository> customFeedRepository = new(MockBehavior.Strict);
        customFeedRepository.Setup(r => r.GetForUserAsync(TestPrefs.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<ICustomFeedFetcher> customFeedFetcher = new(MockBehavior.Strict);

        Mock<IEvaluateNewsUseCase> evaluateNewsUseCase = new(MockBehavior.Strict);
        evaluateNewsUseCase.Setup(e => e.EvaluateArticlesV2(
                It.Is<List<NewsItem>>(items => items.SequenceEqual(new[] { unresolvedItem })),
                TestPrefs,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvaluatedNewsArticles { NewsArticles = [llmArticle] });

        Mock<IGetTranslationUseCase> translationUseCase = new(MockBehavior.Strict);
        translationUseCase.Setup(t => t.TranslateArticleAsync(
                It.Is<List<NewsArticle>>(articles =>
                    articles.Select(a => a.Link).SequenceEqual(new[] { llmArticle.Link })),
                "en",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<NewsArticle> articles, string _, CancellationToken _) => articles);

        GetNewsUseCase sut = new(
            newsService.Object,
            customFeedRepository.Object,
            customFeedFetcher.Object,
            evaluateNewsUseCase.Object,
            translationUseCase.Object);

        List<NewsArticle> result = [];
        List<NewsLoadingPhase> loadingPhases = [];
        await foreach (NewsArticle article in sut.GetNewsStreamAsync(
            TestPrefs,
            CancellationToken.None,
            (phase, _) =>
            {
                loadingPhases.Add(phase);
                return Task.CompletedTask;
            }))
            result.Add(article);

        Assert.Equal([llmArticle.Link], result.Select(a => a.Link));
        Assert.Equal([NewsLoadingPhase.Llm], loadingPhases);
        evaluateNewsUseCase.Verify(e => e.EvaluateArticlesV2(
            It.Is<List<NewsItem>>(items => items.SequenceEqual(new[] { unresolvedItem })),
            TestPrefs,
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
