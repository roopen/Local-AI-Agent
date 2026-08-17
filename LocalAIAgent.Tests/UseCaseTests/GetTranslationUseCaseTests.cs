using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LocalAIAgent.Tests.UseCaseTests;

public class GetTranslationUseCaseTests
{
    /// <summary>Stub source that always requires translation, scoped to one host.</summary>
    private sealed class StubTranslatableSource(string host) : BaseNewsClientSettings
    {
        public override string ClientName => "StubClient";
        public override string BaseUrl => $"https://{host}/";
        public override string Language => "ja";
        public override List<string> GetNewsUrls() => [];
        public override void AddHttpClient(IServiceCollection services) { }
    }

    /// <summary>Stub source publishing in the target language (no translation needed).</summary>
    private sealed class StubEnglishSource : BaseNewsClientSettings
    {
        public override string ClientName => "EnglishClient";
        public override string BaseUrl => "https://english.example/";
        public override string Language => "en";
        public override List<string> GetNewsUrls() => [];
        public override void AddHttpClient(IServiceCollection services) { }
    }

    private static AIOptions Options(bool useResultsForDataset = true) => new()
    {
        ModelId = "test-model",
        EndpointUrl = "http://localhost:1234/v1/",
        UseResultsForDataset = useResultsForDataset,
    };

    private static NewsArticle Article(string title, string summary, string link, string source, string sourceLanguage = "ja") => new()
    {
        Title = title,
        Summary = summary,
        Link = link,
        Source = source,
        SourceLanguage = sourceLanguage,
        PublishedDate = DateTime.UtcNow,
        Categories = [],
        Relevancy = Relevancy.High,
    };

    [Fact]
    public async Task TranslateArticleAsync_AllSourcesMatchTargetLanguage_ReturnsArticlesUnchangedAndDoesNotCallLlm()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new(MockBehavior.Strict);

        // English source + English target = no translation needed.
        GetTranslationUseCase sut = new(
            [new StubEnglishSource()],
            repo.Object,
            new FakeLlmRuntimeManager(Options(), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles = [Article("Hello", "World", "https://english.example/x", "english.example", sourceLanguage: "en")];

        List<NewsArticle> result = await sut.TranslateArticleAsync(articles, "en");

        Assert.Empty(chat.Calls);
        Assert.Equal("Hello", result[0].Title);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TranslateArticleAsync_AllArticlesCached_AppliesCacheAndDoesNotCallLlm()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new(MockBehavior.Strict);
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), "Spanish", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>
            {
                ["https://taiwan.example/a"] = new("Cached Title", "Cached Summary"),
            });

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles = [Article("Original", "Original summary", "https://taiwan.example/a", "taiwan.example")];

        List<NewsArticle> result = await sut.TranslateArticleAsync(articles, "Spanish");

        Assert.Empty(chat.Calls);
        Assert.Equal("Cached Title", result[0].Title);
        Assert.Equal("Cached Summary", result[0].Summary);
    }

    [Fact]
    public async Task TranslateArticleAsync_OnlyArticlesWithDifferentLanguage_AreSentToLlm()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""[{"index":0,"title":"Hola","summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("news.taiwan.tw")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        // Each article carries its own language; the filter is purely article-level.
        List<NewsArticle> articles =
        [
            Article("foreign", "foreign summary", "https://news.taiwan.tw/a", "news.taiwan.tw", sourceLanguage: "ja"),
            Article("english", "english summary", "https://nytimes.com/x", "nytimes.com", sourceLanguage: "es"),
        ];

        await sut.TranslateArticleAsync(articles, "es");

        // Only the foreign-language article (ja) is translated; the matching-language one (es) is skipped.
        Assert.Single(chat.Calls);
        string userPayload = chat.Calls[0].Messages.Last(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text!;
        Assert.Contains("foreign", userPayload);
        Assert.DoesNotContain("english summary", userPayload);
    }

    [Fact]
    public async Task TranslateArticleAsync_UsesInitialBatchesOfFive()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());

        chat.EnqueueStreamingText("""
            [
              {"index":0,"title":"Translated 0","summary":"Summary 0"},
              {"index":1,"title":"Translated 1","summary":"Summary 1"},
              {"index":2,"title":"Translated 2","summary":"Summary 2"},
              {"index":3,"title":"Translated 3","summary":"Summary 3"},
              {"index":4,"title":"Translated 4","summary":"Summary 4"}
            ]
            """);
        chat.EnqueueStreamingText("""[{"index":0,"title":"Translated 5","summary":"Summary 5"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles = [.. Enumerable.Range(0, 6)
            .Select(i => Article($"title {i}", $"summary {i}", $"https://taiwan.example/{i}", "taiwan.example"))];

        await sut.TranslateArticleAsync(articles, "Spanish");

        Assert.Equal(2, chat.Calls.Count);
        string firstPayload = chat.Calls[0].Messages.Last(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text!;
        string secondPayload = chat.Calls[1].Messages.Last(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text!;
        Assert.Contains("title 4", firstPayload);
        Assert.DoesNotContain("title 5", firstPayload);
        Assert.Contains("title 5", secondPayload);
    }

    [Fact]
    public async Task TranslateArticleAsync_SuccessfulResponse_OverwritesTitleAndSummary()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        repo.Setup(r => r.SaveTranslationsAsync(It.IsAny<List<NewsArticle>>(),
                It.IsAny<List<(string OriginalTitle, string OriginalSummary)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chat.EnqueueStreamingText("""[{"index":0,"title":"Hola","summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign title", "foreign summary", "https://taiwan.example/a", "taiwan.example");

        await sut.TranslateArticleAsync([article], "Spanish");

        Assert.Equal("Hola", article.Title);
        Assert.Equal("Mundo", article.Summary);

        RecordedCall call = Assert.Single(chat.Calls);
        Assert.NotNull(call.Options);
        Assert.Equal(0f, call.Options.Temperature.GetValueOrDefault());
        Assert.Equal(0f, call.Options.FrequencyPenalty.GetValueOrDefault());
        Assert.Equal(0f, call.Options.PresencePenalty.GetValueOrDefault());

        ChatResponseFormatJson responseFormat = Assert.IsType<ChatResponseFormatJson>(call.Options.ResponseFormat);
        Assert.True(responseFormat.Schema.HasValue);
        Assert.Equal("array", responseFormat.Schema.Value.GetProperty("type").GetString());

        string systemPrompt = call.Messages.Single(m => m.Role == ChatRole.System).Text!;
        Assert.DoesNotContain("<|think|>", systemPrompt);
        Assert.DoesNotContain("<|channel>", systemPrompt);
        Assert.DoesNotContain("<|turn>", systemPrompt);

        string userPayload = call.Messages.Single(m => m.Role == ChatRole.User).Text!;
        Assert.Contains("\"index\":0", userPayload);
    }

    [Fact]
    public async Task TranslateArticleAsync_PartialIndexedBatchResponse_RetriesOnlyMissingArticle()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        repo.Setup(r => r.SaveTranslationsAsync(It.IsAny<List<NewsArticle>>(),
                It.IsAny<List<(string OriginalTitle, string OriginalSummary)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        chat.EnqueueStreamingText("""
            [
                {"index":0,"title":"Uno","summary":"Uno summary"},
                {"index":2,"title":"Tres","summary":"Tres summary"}
            ]
            """);
        chat.EnqueueStreamingText("""
            [{"index":0,"title":"Dos","summary":"Dos summary"}]
            """);

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles =
        [
            Article("one", "one summary", "https://taiwan.example/a", "taiwan.example"),
            Article("two", "two summary", "https://taiwan.example/b", "taiwan.example"),
            Article("three", "three summary", "https://taiwan.example/c", "taiwan.example"),
        ];

        await sut.TranslateArticleAsync(articles, "Spanish");

        Assert.Equal(["Uno", "Dos", "Tres"], articles.Select(a => a.Title));
        Assert.Equal(2, chat.Calls.Count);

        string retryPayload = chat.Calls[1].Messages.Single(m => m.Role == ChatRole.User).Text!;
        Assert.Contains("\"title\":\"two\"", retryPayload);
        Assert.DoesNotContain("\"title\":\"one\"", retryPayload);
        Assert.DoesNotContain("\"title\":\"three\"", retryPayload);

        repo.Verify(r => r.SaveTranslationsAsync(
            It.Is<List<NewsArticle>>(batch => batch.Count == 2),
            It.IsAny<List<(string, string)>>(),
            "Spanish",
            It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveTranslationsAsync(
            It.Is<List<NewsArticle>>(batch => batch.Count == 1),
            It.IsAny<List<(string, string)>>(),
            "Spanish",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranslateArticleAsync_SingleArticleFailure_RetriesOnce()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());

        chat.EnqueueStreamingText("not json");
        chat.EnqueueStreamingText("""[{"index":0,"title":"Hola","summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign", "summary", "https://taiwan.example/a", "taiwan.example");
        await sut.TranslateArticleAsync([article], "Spanish");

        Assert.Equal("Hola", article.Title);
        Assert.Equal(2, chat.Calls.Count);
    }

    [Fact]
    public async Task TranslateArticleAsync_CompleteUnindexedArray_UsesArrayOrderWithoutRetry()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""
            [
                {"title":"Uno","summary":"Uno summary"},
                {"title":"Dos","summary":"Dos summary"}
            ]
            """);

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles =
        [
            Article("one", "one summary", "https://taiwan.example/a", "taiwan.example"),
            Article("two", "two summary", "https://taiwan.example/b", "taiwan.example"),
        ];

        await sut.TranslateArticleAsync(articles, "Spanish");

        Assert.Equal(["Uno", "Dos"], articles.Select(article => article.Title));
        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task TranslateArticleAsync_SingleObjectResponse_AcceptsUnambiguousTranslation()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""{"title":"Hola","summary":"Mundo"}""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article(
            "foreign",
            "foreign summary",
            "https://taiwan.example/a",
            "taiwan.example");

        await sut.TranslateArticleAsync([article], "Spanish");

        Assert.Equal("Hola", article.Title);
        Assert.Equal("Mundo", article.Summary);
        Assert.Single(chat.Calls);
    }

    [Fact]
    public async Task TranslateArticleAsync_RequestFailure_PropagatesToCaller()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign", "summary", "https://taiwan.example/a", "taiwan.example");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.TranslateArticleAsync([article], "Spanish"));

        Assert.Contains("no streaming response was queued", exception.Message);
    }

    [Fact]
    public async Task TranslateArticleAsync_UseResultsForDatasetFalse_DoesNotCallSave()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new(MockBehavior.Strict);
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""[{"index":0,"title":"Hola","summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(useResultsForDataset: false), chat),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign", "summary", "https://taiwan.example/a", "taiwan.example");
        await sut.TranslateArticleAsync([article], "Spanish");

        repo.Verify(r => r.SaveTranslationsAsync(
            It.IsAny<List<NewsArticle>>(),
            It.IsAny<List<(string, string)>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranslateArticleAsync_PreCanceledRequestDoesNotCallLlmOrRepository()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new(MockBehavior.Strict);
        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            new FakeLlmRuntimeManager(Options(), chat),
            NullLogger<GetTranslationUseCase>.Instance);
        NewsArticle article = Article(
            "foreign",
            "summary",
            "https://taiwan.example/a",
            "taiwan.example");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.TranslateArticleAsync([article], "Spanish", cancellation.Token));

        Assert.Empty(chat.Calls);
        repo.VerifyNoOtherCalls();
    }
}
