using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.Reader;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LocalAIAgent.Tests.IntegrationTests;

public class ArticleReaderPersistentCacheTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "reader-cache-" + Guid.NewGuid().ToString("N"));
    private string Database => Path.Combine(directory, "cache.db");
    private static readonly ArticleReaderCacheKey Key = new(1, "https://example.com/article", "en", "http://model/v1", "test-model");
    private static ReadArticleResult Result => new()
    {
        Original = new() { Title = "Otsikko", Markdown = "Alkuperäinen teksti.", SourceUrl = Key.Url, Status = "complete" },
        TargetLanguage = "en", DetectedLanguage = "fi", TranslationStatus = "complete",
        TranslatedTitle = "Title", TranslatedMarkdown = "Translated article.",
    };
    private ArticleReaderPersistentCache Cache(TimeProvider? clock = null) => new(Database, clock ?? TimeProvider.System,
        NullLogger<ArticleReaderPersistentCache>.Instance);

    [Fact]
    public async Task SavedTranslationSurvivesNewReaderAndMemoryCacheWithoutFetchingOrTranslating()
    {
        using ArticleReaderResources firstResources = new();
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"fi\"}");
        chat.EnqueueResponseText("""{"blocks":[{"id":0,"text":"Title"},{"id":1,"text":"Translated article."}]}""");
        AIOptions options = new() { EndpointUrl = Key.Endpoint, ModelId = Key.Model };
        Mock<IArticleBrowser> browser = new();
        browser.Setup(b => b.ExtractAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Original);
        Mock<IArticleBrowserFactory> factory = new();
        factory.Setup(b => b.OpenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(browser.Object);
        ReadArticleUseCase first = new(factory.Object, new(NullLogger<ArticleReaderAgent>.Instance),
            new(NullLogger<ArticleBodyTranslator>.Instance), new FakeLlmRuntimeManager(options, chat), firstResources,
            NullLogger<ReadArticleUseCase>.Instance, Cache());
        ReadArticleResult original = await first.ReadAsync(new(Key.Url), 1, 1, "en", TestContext.Current.CancellationToken);
        Assert.Equal("complete", original.TranslationStatus);

        using ArticleReaderResources restartedResources = new();
        Mock<IArticleBrowserFactory> unusedBrowser = new(MockBehavior.Strict);
        FakeChatClient unusedModel = new();
        ReadArticleUseCase restarted = new(unusedBrowser.Object, new(NullLogger<ArticleReaderAgent>.Instance),
            new(NullLogger<ArticleBodyTranslator>.Instance), new FakeLlmRuntimeManager(options, unusedModel), restartedResources,
            NullLogger<ReadArticleUseCase>.Instance, Cache());
        ReadArticleResult saved = await restarted.ReadWithProgressAsync(new(Key.Url + "#section"), 1, 1, "en",
            _ => throw new InvalidOperationException("Cache hit must not start loading phases."), TestContext.Current.CancellationToken);
        Assert.Equal(original, saved);
        unusedBrowser.VerifyNoOtherCalls();
        Assert.Empty(unusedModel.Calls);
    }

    [Fact]
    public async Task CacheIsScopedByUserUrlLanguageModelAndEndpoint()
    {
        await Cache().SetAsync(Key, new(Result, DateTimeOffset.UtcNow.AddMinutes(30)), TestContext.Current.CancellationToken);
        foreach (ArticleReaderCacheKey other in new[] { Key with { UserId = 2 }, Key with { Url = Key.Url + "/other" },
            Key with { TargetLanguage = "fi" }, Key with { Model = "other" }, Key with { Endpoint = "http://other/v1" } })
            Assert.Null(await Cache().GetAsync(other, TestContext.Current.CancellationToken));
        Assert.Equal(Result, (await Cache().GetAsync(Key, TestContext.Current.CancellationToken))?.Result);
    }

    [Fact]
    public async Task ExpiryDoesNotExtendWhenReadOrRestarted()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await Cache(new FixedClock(now)).SetAsync(Key, new(Result, now.AddMinutes(30)), TestContext.Current.CancellationToken);
        Assert.NotNull(await Cache(new FixedClock(now.AddMinutes(29))).GetAsync(Key, TestContext.Current.CancellationToken));
        Assert.Null(await Cache(new FixedClock(now.AddMinutes(31))).GetAsync(Key, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("partial", "complete")]
    [InlineData("blocked", "complete")]
    [InlineData("complete", "failed")]
    public async Task IncompleteResultsAreNotPersisted(string extraction, string translation)
    {
        ReadArticleResult result = Result with { Original = Result.Original with { Status = extraction }, TranslationStatus = translation };
        await Cache().SetAsync(Key, new(result, DateTimeOffset.UtcNow.AddMinutes(30)), TestContext.Current.CancellationToken);
        Assert.Null(await Cache().GetAsync(Key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PerUserEntryLimitEvictsOldestResult()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 0; i < 65; i++)
            await Cache().SetAsync(Key with { Url = Key.Url + i }, new(Result, now.AddMinutes(20).AddSeconds(i)), TestContext.Current.CancellationToken);
        Assert.Null(await Cache().GetAsync(Key with { Url = Key.Url + 0 }, TestContext.Current.CancellationToken));
        Assert.NotNull(await Cache().GetAsync(Key with { Url = Key.Url + 64 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CorruptCacheIsAMissAndDoesNotFailArticleRetrieval()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Database, "not a database", TestContext.Current.CancellationToken);
        Assert.Null(await Cache().GetAsync(Key, TestContext.Current.CancellationToken));
        await Cache().SetAsync(Key, new(Result, DateTimeOffset.UtcNow.AddMinutes(30)), TestContext.Current.CancellationToken);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public void Dispose()
    {
        if (File.Exists(Database)) File.Delete(Database);
        if (Directory.Exists(directory)) Directory.Delete(directory);
    }
}
