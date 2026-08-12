using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.Tests.UseCaseTests;

public class EvaluateNewsUseCaseTests
{
    private static readonly UserPreferences TestPrefs = new()
    {
        Id = 7,
        Prompt = "Be helpful.",
        Interests = ["AI"],
        Dislikes = [],
    };

    private static readonly AIOptions TestOptions = new()
    {
        ModelId = "test-model",
        EndpointUrl = "http://localhost:1234/v1/",
        UseResultsForDataset = true,
        Temperature = 0.2m,
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

    private static (EvaluateNewsUseCase, FakeChatClient, Mock<INewsDatasetRepository>) BuildSut(
        Action<Mock<INewsDatasetRepository>>? configureRepo = null)
    {
        FakeChatClient chat = new();
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        Mock<INewsDatasetRepository> repo = new(MockBehavior.Strict);
        repo.Setup(r => r.GetCachedEvaluationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedNewsEvaluation>());
        repo.Setup(r => r.SaveAsync(It.IsAny<List<NewsArticle>>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        configureRepo?.Invoke(repo);

        EvaluateNewsUseCase sut = new(
            new FakeLlmRuntimeManager(TestOptions, chat),
            cache,
            repo.Object,
            NullLogger<EvaluateNewsUseCase>.Instance);
        return (sut, chat, repo);
    }

    [Fact]
    public async Task EvaluateArticlesV2_AllArticlesCached_DoesNotCallLlm()
    {
        NewsItem a = MakeItem("AI breakthrough", "summary", "https://x.com/a");
        NewsItem b = MakeItem("Stock report", "summary", "https://x.com/b");

        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut(repo =>
        {
            repo.Setup(r => r.GetCachedEvaluationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, CachedNewsEvaluation>
                {
                    ["https://x.com/a"] = new(Relevancy.High, "Tech", null, "test-model"),
                    ["https://x.com/b"] = new(Relevancy.Low, "Finance", null, "test-model"),
                });
        });

        EvaluatedNewsArticles result = await sut.EvaluateArticlesV2([a, b], TestPrefs);

        Assert.Empty(chat.Calls);
        Assert.Equal(2, result.NewsArticles.Count);
    }

    [Fact]
    public async Task EvaluateArticlesV2_NoCache_CallsLlmAndReturnsParsedArticles()
    {
        NewsItem item = MakeItem("AI breakthrough", "summary", "https://x.com/a");

        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut();
        chat.EnqueueStreamingText("""[{"ArticleIndex":0,"Relevancy":"High","Topic":"Tech"}]""");

        EvaluatedNewsArticles result = await sut.EvaluateArticlesV2([item], TestPrefs);

        Assert.Single(chat.Calls);
        NewsArticle only = Assert.Single(result.NewsArticles);
        Assert.Equal(Relevancy.High, only.Relevancy);
        Assert.Equal("Tech", only.Topic);
        Assert.Equal("https://x.com/a", only.Link);
    }

    [Fact]
    public async Task EvaluateArticlesV2_PartiallyCached_CallsLlmOnlyForUncachedItems()
    {
        NewsItem cached = MakeItem("Cached", "x", "https://x.com/cached");
        NewsItem fresh = MakeItem("Fresh", "y", "https://x.com/fresh");

        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut(repo =>
        {
            repo.Setup(r => r.GetCachedEvaluationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, CachedNewsEvaluation>
                {
                    ["https://x.com/cached"] = new(Relevancy.High, "Tech", null, "test-model"),
                });
        });
        chat.EnqueueStreamingText("""[{"ArticleIndex":0,"Relevancy":"Low","Topic":"Sports"}]""");

        EvaluatedNewsArticles result = await sut.EvaluateArticlesV2([cached, fresh], TestPrefs);

        // Exactly one LLM call (only for the uncached item).
        Assert.Single(chat.Calls);
        // The single user message must contain only the fresh article's content.
        string userMessage = chat.Calls[0].Messages.Last(m => m.Role == ChatRole.User).Text!;
        Assert.Contains("Fresh", userMessage);
        Assert.DoesNotContain("Cached", userMessage);
        Assert.Equal(2, result.NewsArticles.Count);
    }

    [Fact]
    public async Task EvaluateArticlesV2_BatchesArticlesInGroupsOfThree()
    {
        // Seven articles → three batches of (3, 3, 1) → three LLM calls.
        List<NewsItem> articles = [.. Enumerable.Range(0, 7).Select(i => MakeItem($"Title {i}", $"Summary {i}", $"https://x.com/{i}"))];
        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut();

        for (int i = 0; i < 3; i++)
        {
            chat.EnqueueStreamingText("""[{"ArticleIndex":0,"Relevancy":"Low"}]""");
        }

        await sut.EvaluateArticlesV2(articles, TestPrefs);

        Assert.Equal(3, chat.Calls.Count);
    }

    [Fact]
    public async Task EvaluateArticlesV2_DeserializationFailure_DoesNotThrowAndSkipsBatch()
    {
        NewsItem item = MakeItem("Title", "Summary", "https://x.com/a");

        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut();
        chat.EnqueueStreamingText("not valid json at all");

        EvaluatedNewsArticles result = await sut.EvaluateArticlesV2([item], TestPrefs);

        // No exception, no parsed articles for the bad batch.
        Assert.Empty(result.NewsArticles);
    }

    [Fact]
    public async Task EvaluateArticlesV2_PassesUserPreferencesIdAndModelToSave()
    {
        NewsItem item = MakeItem("Title", "Summary", "https://x.com/a");

        (EvaluateNewsUseCase sut, _, Mock<INewsDatasetRepository> repo) = BuildSut();
        FakeChatClient chat = new();
        chat.EnqueueStreamingText("""[{"ArticleIndex":0,"Relevancy":"High"}]""");
        sut = new EvaluateNewsUseCase(
            new FakeLlmRuntimeManager(TestOptions, chat),
            new MemoryCache(new MemoryCacheOptions()),
            repo.Object,
            NullLogger<EvaluateNewsUseCase>.Instance);

        await sut.EvaluateArticlesV2([item], TestPrefs);

        repo.Verify(r => r.SaveAsync(
            It.IsAny<List<NewsArticle>>(),
            TestPrefs.Id,
            TestOptions.UseResultsForDataset,
            TestOptions.ModelId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateArticlesV2_AggregatesUsageDetailsFromStream()
    {
        NewsItem item = MakeItem("Title", "Summary", "https://x.com/a");

        (EvaluateNewsUseCase sut, FakeChatClient chat, _) = BuildSut();
        chat.EnqueueStreaming(
            new ChatResponseUpdate(ChatRole.Assistant, """[{"ArticleIndex":0,"Relevancy":"High"}]"""),
            new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents =
                [
                    new UsageContent(new UsageDetails
                    {
                        InputTokenCount = 42,
                        OutputTokenCount = 13,
                        TotalTokenCount = 55,
                    })
                ]
            });

        EvaluatedNewsArticles result = await sut.EvaluateArticlesV2([item], TestPrefs);

        NewsArticle only = Assert.Single(result.NewsArticles);
        Assert.Equal(42, only.InputTokens);
        Assert.Equal(13, only.OutputTokens);
    }
}
