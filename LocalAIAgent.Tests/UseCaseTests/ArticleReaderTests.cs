using System.Text.Json;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.Reader;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.AI;
using Moq;

namespace LocalAIAgent.Tests.UseCaseTests;

public class ArticleReaderTests
{
    private static readonly AIOptions Options = new() { ModelId = "reader-test", EndpointUrl = "http://model/v1" };
    private static ArticleContent Article(string text = "A complete original article.") => new()
    {
        Title = "Title", Markdown = text, Status = "complete", SourceUrl = "https://example.com/article", Language = "fi",
    };
    private static ArticleBodyTranslator Translator() => new(NullLogger<ArticleBodyTranslator>.Instance);

    [Fact]
    public async Task ActualLanguageOverridesMetadataAndSkipsUnnecessaryTranslation()
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"en\"}");
        List<ArticleReaderProgress> progress = [];
        ReadArticleResult result = await Translator().TranslateAsync(Article(), "fi", "en", new(Options, chat), default,
            value => { progress.Add(value); return Task.CompletedTask; });
        Assert.Equal("notNeeded", result.TranslationStatus);
        Assert.Equal("en", result.DetectedLanguage);
        Assert.Null(result.TranslatedMarkdown);
        Assert.Single(chat.Calls);
        Assert.Equal(["detecting"], progress.Select(value => value.Phase));
    }

    [Theory]
    [InlineData("und")]
    [InlineData("not a language")]
    public async Task UnknownLanguagePreservesOriginal(string language)
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText(JsonSerializer.Serialize(new { language }));
        ReadArticleResult result = await Translator().TranslateAsync(Article(), null, "en", new(Options, chat), default);
        Assert.Equal("failed", result.TranslationStatus);
        Assert.Equal(Article().Markdown, result.Original.Markdown);
    }

    [Fact]
    public async Task TranslationOrdersBlocksAndRequiresEveryBlock()
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"fi\"}");
        chat.EnqueueResponseText("""{"blocks":[{"id":2,"text":"Second"},{"id":0,"text":"Translated title"},{"id":1,"text":"First"}]}""");
        ReadArticleResult result = await Translator().TranslateAsync(Article("Yksi\n\nKaksi"), null, "en", new(Options, chat), default);
        Assert.Equal("complete", result.TranslationStatus);
        Assert.Equal("First\n\nSecond", result.TranslatedMarkdown);
        Assert.Equal("Translated title", result.TranslatedTitle);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"blocks\":[]}")]
    [InlineData("{\"blocks\":[{\"id\":0,\"text\":\"title\"},{\"id\":0,\"text\":\"body\"}]}")]
    public async Task InvalidOrIncompleteTranslationNeverReturnsPartialTranslation(string response)
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"fi\"}");
        chat.EnqueueResponseText(response);
        ReadArticleResult result = await Translator().TranslateAsync(Article(), null, "en", new(Options, chat), default);
        Assert.Equal("failed", result.TranslationStatus);
        Assert.Null(result.TranslatedMarkdown);
        Assert.Equal(Article(), result.Original);
    }

    [Fact]
    public async Task LongArticleIsTranslatedInBoundedOrderedBatches()
    {
        string text = string.Join("\n\n", Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), 3500)));
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"fi\"}");
        chat.EnqueueResponseText(JsonSerializer.Serialize(new { blocks = new[] { new { id = 0, text = "Title" }, new { id = 1, text = "part1" } } }));
        for (int i = 2; i <= 8; i++) chat.EnqueueResponseText(JsonSerializer.Serialize(new { blocks = new[] { new { id = i, text = "part" + i } } }));
        List<ArticleReaderProgress> progress = [];
        ReadArticleResult result = await Translator().TranslateAsync(Article(text), null, "en", new(Options, chat), default,
            value => { progress.Add(value); return Task.CompletedTask; });
        Assert.Equal("complete", result.TranslationStatus);
        Assert.Equal(string.Join("\n\n", Enumerable.Range(1, 8).Select(i => "part" + i)), result.TranslatedMarkdown);
        Assert.All(chat.Calls.Skip(1), call => Assert.True(call.Messages.Last().Text.Length < 6500));
        Assert.Equal("detecting", progress[0].Phase);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => new ArticleReaderProgress("translating", i, 8)), progress.Skip(1));
    }

    [Fact]
    public void SplittingOversizedParagraphPreservesAllCharacters()
    {
        string text = new string('a', 3999) + "😀" + new string('b', 5000);
        string[] chunks = ArticleBodyTranslator.SplitText(text).ToArray();
        Assert.Equal(text, string.Concat(chunks));
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 4000));
        Assert.All(chunks, chunk => Assert.False(char.IsHighSurrogate(chunk[^1])));
    }

    [Fact]
    public async Task ProviderWithoutToolSupportRetainsDeterministicContent()
    {
        Mock<IArticleBrowser> browser = new();
        ArticleContent original = Article() with { Status = "partial" };
        browser.Setup(b => b.ExtractAsync(It.IsAny<CancellationToken>())).ReturnsAsync(original);
        // An unconfigured fake throws when recovery attempts to invoke the model.
        ArticleReaderAgent agent = new(NullLogger<ArticleReaderAgent>.Instance);
        Assert.Equal(original, await agent.ExtractAsync(browser.Object, original.SourceUrl, new(Options, new FakeChatClient()), default));
    }

    [Fact]
    public async Task RecoveryCannotExceedTwelveTools()
    {
        Mock<IArticleBrowser> browser = new();
        browser.Setup(b => b.ExtractAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Article() with { Status = "partial" });
        FakeChatClient chat = new();
        for (int i = 0; i < 20; i++) chat.EnqueueResponse(new(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent(i.ToString(), "wait_for_article", new Dictionary<string, object?>())])));
        await new ArticleReaderAgent(NullLogger<ArticleReaderAgent>.Instance).ExtractAsync(browser.Object, Article().SourceUrl, new(Options, chat), default);
        browser.Verify(b => b.WaitAsync(It.IsAny<CancellationToken>()), Times.Exactly(12));
        Assert.Equal(12, chat.Calls.Count);
    }

    [Fact]
    public async Task CacheIsPartitionedByUserAndReaderDisposesItsBrowser()
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("{\"language\":\"en\"}");
        chat.EnqueueResponseText("{\"language\":\"en\"}");
        Mock<IArticleBrowser> browser = new();
        browser.Setup(b => b.ExtractAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Article());
        Mock<IArticleBrowserFactory> factory = new();
        factory.Setup(f => f.OpenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(browser.Object);
        using ArticleReaderResources resources = new();
        FakeLlmRuntimeManager runtime = new(Options, chat);
        ReadArticleUseCase reader = new(factory.Object, new(NullLogger<ArticleReaderAgent>.Instance), Translator(), runtime,
            resources, NullLogger<ReadArticleUseCase>.Instance);
        ReadArticleRequest request = new(Article().SourceUrl);
        await reader.ReadAsync(request, 1, 1, "en", default);
        await reader.ReadAsync(request, 1, 1, "en", default);
        await reader.ReadAsync(request, 2, 2, "en", default);
        factory.Verify(f => f.OpenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        browser.Verify(b => b.DisposeAsync(), Times.Exactly(2));
        Assert.Equal(0, runtime.WarmUpCalls);
    }

    [Fact]
    public async Task CancellingQueuedReaderDoesNotAffectActiveSessions()
    {
        using ArticleReaderResources resources = new();
        await resources.Browsers.WaitAsync();
        await resources.Browsers.WaitAsync();
        Mock<IArticleBrowserFactory> factory = new(MockBehavior.Strict);
        ReadArticleUseCase reader = new(factory.Object, new(NullLogger<ArticleReaderAgent>.Instance), Translator(),
            new FakeLlmRuntimeManager(Options, new FakeChatClient()), resources, NullLogger<ReadArticleUseCase>.Instance);
        using CancellationTokenSource cancellation = new();
        Task pending = reader.ReadAsync(new(Article().SourceUrl), 1, 1, "en", cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, resources.Browsers.CurrentCount);
        factory.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("http://localhost/article")]
    [InlineData("https://user:password@example.com")]
    [InlineData("http://example.com:8080")]
    public void UnsupportedUrlsAreRejected(string url) => Assert.False(ArticleUrlPolicy.IsValid(url));

    [Fact]
    public async Task CancellingActiveReaderDisposesSessionAndRestoresCapacity()
    {
        using ArticleReaderResources resources = new();
        Mock<IArticleBrowser> browser = new();
        browser.Setup(b => b.NavigateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => Task.Delay(Timeout.Infinite, ct));
        Mock<IArticleBrowserFactory> factory = new();
        factory.Setup(f => f.OpenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(browser.Object);
        ReadArticleUseCase reader = new(factory.Object, new(NullLogger<ArticleReaderAgent>.Instance), Translator(),
            new FakeLlmRuntimeManager(Options, new FakeChatClient()), resources, NullLogger<ReadArticleUseCase>.Instance);
        using CancellationTokenSource cancellation = new();
        Task pending = reader.ReadAsync(new(Article().SourceUrl), 1, 1, "en", cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(2, resources.Browsers.CurrentCount);
        browser.Verify(b => b.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:192.168.1.1")]
    public async Task SharedProxyConnectorRejectsNonPublicAddresses(string host)
    {
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await PublicNetworkHttpHandler.ConnectPublicAsync(new System.Net.DnsEndPoint(host, 443), default));
    }
}
