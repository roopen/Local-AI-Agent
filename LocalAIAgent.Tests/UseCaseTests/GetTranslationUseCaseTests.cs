using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
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

    private static NewsArticle Article(string title, string summary, string link, string source) => new()
    {
        Title = title,
        Summary = summary,
        Link = link,
        Source = source,
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
            chat,
            Options(),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles = [Article("Hello", "World", "https://english.example/x", "english.example")];

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
            chat,
            Options(),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles = [Article("Original", "Original summary", "https://taiwan.example/a", "taiwan.example")];

        List<NewsArticle> result = await sut.TranslateArticleAsync(articles, "Spanish");

        Assert.Empty(chat.Calls);
        Assert.Equal("Cached Title", result[0].Title);
        Assert.Equal("Cached Summary", result[0].Summary);
    }

    [Fact]
    public async Task TranslateArticleAsync_OnlyArticlesFromTranslatableHosts_AreSentToLlm()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new();
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""[{"Title":"Hola","Summary":"Mundo"}]""");

        // Hosts deliberately don't share any parent domain, otherwise MatchesHost's
        // 1-level parent strip lets them match each other (see MatchesHostTests).
        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("news.taiwan.tw")],
            repo.Object,
            chat,
            Options(useResultsForDataset: false),
            NullLogger<GetTranslationUseCase>.Instance);

        List<NewsArticle> articles =
        [
            Article("foreign", "foreign summary", "https://news.taiwan.tw/a", "news.taiwan.tw"),
            Article("english", "english summary", "https://nytimes.com/x", "nytimes.com"),
        ];

        await sut.TranslateArticleAsync(articles, "Spanish");

        // The LLM is only invoked for the foreign article.
        Assert.Single(chat.Calls);
        string userPayload = chat.Calls[0].Messages.Last(m => m.Role == Microsoft.Extensions.AI.ChatRole.User).Text!;
        Assert.Contains("foreign", userPayload);
        Assert.DoesNotContain("english summary", userPayload);
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
        chat.EnqueueStreamingText("""[{"Title":"Hola","Summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            chat,
            Options(),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign title", "foreign summary", "https://taiwan.example/a", "taiwan.example");

        await sut.TranslateArticleAsync([article], "Spanish");

        Assert.Equal("Hola", article.Title);
        Assert.Equal("Mundo", article.Summary);
    }

    [Fact]
    public async Task TranslateArticleAsync_UseResultsForDatasetFalse_DoesNotCallSave()
    {
        FakeChatClient chat = new();
        Mock<IArticleTranslationRepository> repo = new(MockBehavior.Strict);
        repo.Setup(r => r.GetCachedTranslationsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, CachedTranslation>());
        chat.EnqueueStreamingText("""[{"Title":"Hola","Summary":"Mundo"}]""");

        GetTranslationUseCase sut = new(
            [new StubTranslatableSource("taiwan.example")],
            repo.Object,
            chat,
            Options(useResultsForDataset: false),
            NullLogger<GetTranslationUseCase>.Instance);

        NewsArticle article = Article("foreign", "summary", "https://taiwan.example/a", "taiwan.example");
        await sut.TranslateArticleAsync([article], "Spanish");

        repo.Verify(r => r.SaveTranslationsAsync(
            It.IsAny<List<NewsArticle>>(),
            It.IsAny<List<(string, string)>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
