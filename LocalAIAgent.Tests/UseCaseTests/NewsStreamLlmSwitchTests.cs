using System.Runtime.CompilerServices;
using System.ServiceModel.Syndication;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LocalAIAgent.Tests.UseCaseTests;

public sealed class NewsStreamLlmSwitchTests
{
    [Fact]
    public async Task EvaluationUsesNewSelectionForNextBatch()
    {
        const int preferencesId = 7;
        FakeLlmRuntimeManager runtime = null!;
        CallbackChatClient firstClient = new(
            () => runtime.SetUserSelection(preferencesId, settingsId: 2),
            """
            [
              {"ArticleIndex":0,"Relevancy":"High","Topic":"One"},
              {"ArticleIndex":1,"Relevancy":"High","Topic":"Two"},
              {"ArticleIndex":2,"Relevancy":"High","Topic":"Three"}
            ]
            """);
        FakeChatClient secondClient = new();
        secondClient.EnqueueStreamingText(
            """[{"ArticleIndex":0,"Relevancy":"High","Topic":"Four"}]""");

        runtime = new FakeLlmRuntimeManager(Options("first-model"), firstClient);
        runtime.Activate(1, new LlmRuntimeSnapshot(Options("first-model", useResultsForDataset: true), firstClient));
        runtime.Activate(2, new LlmRuntimeSnapshot(Options("second-model"), secondClient));
        runtime.SetUserSelection(preferencesId, settingsId: 1);

        Mock<INewsDatasetRepository> repository = new(MockBehavior.Strict);
        repository
            .Setup(item => item.GetCachedEvaluationsAsync(
                It.IsAny<IEnumerable<string>>(),
                preferencesId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedNewsEvaluation>());
        repository
            .Setup(item => item.SaveAsync(
                It.IsAny<List<NewsArticle>>(),
                preferencesId,
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        EvaluateNewsUseCase useCase = new(
            runtime,
            cache,
            repository.Object,
            NullLogger<EvaluateNewsUseCase>.Instance);
        UserPreferences preferences = new()
        {
            Id = preferencesId,
            Prompt = "prompt",
            Interests = ["AI"],
            Dislikes = [],
        };
        List<NewsItem> articles =
        [
            MakeItem(0),
            MakeItem(1),
            MakeItem(2),
            MakeItem(3),
        ];

        EvaluatedNewsArticles result = await useCase.EvaluateArticlesV2(
            articles,
            preferences,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, firstClient.StreamingCallCount);
        Assert.Single(secondClient.Calls, call => call.Streaming);
        Assert.Equal(4, result.NewsArticles.Count);
        repository.Verify(item => item.SaveAsync(
            It.Is<List<NewsArticle>>(saved => saved.Count == 3 && saved.All(article => article.Link != "https://example.com/3")),
            preferencesId, true, "first-model", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.SaveAsync(
            It.Is<List<NewsArticle>>(saved => saved.Count == 1 && saved[0].Link == "https://example.com/3"),
            preferencesId, false, "second-model", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AIOptions Options(string modelId, bool useResultsForDataset = false) => new()
    {
        ModelId = modelId,
        UseResultsForDataset = useResultsForDataset,
        EndpointUrl = "http://localhost:1234/v1/",
        Temperature = 0.2m,
        TopP = 1m,
    };

    private static NewsItem MakeItem(int index)
    {
        SyndicationItem item = new()
        {
            Title = new TextSyndicationContent($"Title {index}"),
            Summary = new TextSyndicationContent($"Summary {index}"),
            PublishDate = DateTimeOffset.UtcNow,
        };
        item.Links.Add(new SyndicationLink(new Uri($"https://example.com/{index}")));
        return new NewsItem(item);
    }

    private sealed class CallbackChatClient(
        Action callback,
        string response) : IChatClient
    {
        public int StreamingCallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingCallCount++;
            callback();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, response);
            await Task.Yield();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
