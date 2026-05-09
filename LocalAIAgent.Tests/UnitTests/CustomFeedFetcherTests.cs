using LocalAIAgent.Application.News;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;

namespace LocalAIAgent.Tests.UnitTests;

public class CustomFeedFetcherTests
{
    private const string SampleRss =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<rss version=\"2.0\">" +
        "<channel>" +
        "<title>Sample Feed</title>" +
        "<link>https://example.com</link>" +
        "<description>Test</description>" +
        "<item>" +
            "<title>First headline</title>" +
            "<link>https://example.com/article-1</link>" +
            "<description>First summary</description>" +
        "</item>" +
        "<item>" +
            "<title>Second headline</title>" +
            "<link>https://example.com/article-2</link>" +
            "<description>Second summary</description>" +
        "</item>" +
        "</channel>" +
        "</rss>";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }

    private static (CustomFeedFetcher fetcher, Mock<ICustomFeedRepository> repo, StubHandler handler)
        BuildSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        StubHandler handler = new(respond);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });

        Mock<ICustomFeedRepository> repo = new();
        CustomFeedFetcher sut = new(factory.Object, repo.Object, NullLogger<CustomFeedFetcher>.Instance);
        return (sut, repo, handler);
    }

    [Fact]
    public async Task FetchAsync_SuccessfulResponse_ReturnsTaggedNewsItems()
    {
        (CustomFeedFetcher sut, Mock<ICustomFeedRepository> repo, StubHandler handler) = BuildSut(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") });

        CustomFeedDescriptor feed = new(Id: 42, Urls: ["https://example.com/feed.xml"], DisplayName: "Example", Language: "ja", Enabled: true, LastFetchErrorMessage: null);

        List<NewsItem> items = await sut.FetchAsync([feed], TestContext.Current.CancellationToken);

        Assert.Equal(2, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal("custom:42", item.SourceClientName);
            Assert.Equal("ja", item.Language);
        });
        Assert.Single(handler.RequestedUrls);
        repo.Verify(r => r.RecordFetchResultAsync(42, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_HttpError_RecordsErrorAndReturnsEmpty()
    {
        (CustomFeedFetcher sut, Mock<ICustomFeedRepository> repo, _) = BuildSut(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        CustomFeedDescriptor feed = new(Id: 1, Urls: ["https://example.com/feed.xml"], DisplayName: "Example", Language: "en", Enabled: true, LastFetchErrorMessage: null);

        List<NewsItem> items = await sut.FetchAsync([feed], TestContext.Current.CancellationToken);

        Assert.Empty(items);
        repo.Verify(r => r.RecordFetchResultAsync(1, It.Is<string>(s => !string.IsNullOrEmpty(s)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_OneFeedFailing_DoesNotAbortOthers()
    {
        StubHandler handler = new(req =>
            req.RequestUri!.ToString().Contains("good", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });
        Mock<ICustomFeedRepository> repo = new();

        CustomFeedFetcher sut = new(factory.Object, repo.Object, NullLogger<CustomFeedFetcher>.Instance);

        CustomFeedDescriptor good = new(Id: 1, Urls: ["https://example.com/good.xml"], DisplayName: "Good", Language: "en", Enabled: true, LastFetchErrorMessage: null);
        CustomFeedDescriptor bad = new(Id: 2, Urls: ["https://example.com/bad.xml"], DisplayName: "Bad", Language: "en", Enabled: true, LastFetchErrorMessage: null);

        List<NewsItem> items = await sut.FetchAsync([good, bad], TestContext.Current.CancellationToken);

        // The good feed yields its items, the bad one yields none — not throws.
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal("custom:1", item.SourceClientName));
    }

    [Fact]
    public async Task FetchAsync_EmptyInput_NoHttpCalls()
    {
        (CustomFeedFetcher sut, _, StubHandler handler) = BuildSut(_ => new HttpResponseMessage(HttpStatusCode.OK));

        List<NewsItem> items = await sut.FetchAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(items);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task FetchAsync_FeedWithMultipleUrls_AggregatesAllItemsAndCallsEachUrl()
    {
        (CustomFeedFetcher sut, Mock<ICustomFeedRepository> repo, StubHandler handler) = BuildSut(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") });

        CustomFeedDescriptor feed = new(
            Id: 7,
            Urls: ["https://example.com/feed-a.xml", "https://example.com/feed-b.xml", "https://example.com/feed-c.xml"],
            DisplayName: "Multi", Language: "en", Enabled: true, LastFetchErrorMessage: null);

        List<NewsItem> items = await sut.FetchAsync([feed], TestContext.Current.CancellationToken);

        // Each URL contributes 2 items from the SampleRss fixture → 6 total.
        Assert.Equal(6, items.Count);
        Assert.Equal(3, handler.RequestedUrls.Count);
        Assert.All(items, item => Assert.Equal("custom:7", item.SourceClientName));
        repo.Verify(r => r.RecordFetchResultAsync(7, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_OneUrlInFeedFailing_RecordsErrorButYieldsItemsFromOthers()
    {
        StubHandler handler = new(req =>
            req.RequestUri!.ToString().Contains("good", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });
        Mock<ICustomFeedRepository> repo = new();
        CustomFeedFetcher sut = new(factory.Object, repo.Object, NullLogger<CustomFeedFetcher>.Instance);

        CustomFeedDescriptor feed = new(
            Id: 9,
            Urls: ["https://example.com/good.xml", "https://example.com/bad.xml"],
            DisplayName: "Mixed", Language: "en", Enabled: true, LastFetchErrorMessage: null);

        List<NewsItem> items = await sut.FetchAsync([feed], TestContext.Current.CancellationToken);

        // Good URL yields 2 items; bad URL fails silently for this feed entry.
        Assert.Equal(2, items.Count);
        // The error message stored on the feed mentions the failing URL so the user can fix it.
        repo.Verify(r => r.RecordFetchResultAsync(
            9,
            It.Is<string>(s => s != null && s.Contains("bad.xml", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
