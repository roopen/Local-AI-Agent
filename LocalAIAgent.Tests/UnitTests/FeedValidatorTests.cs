using LocalAIAgent.Application.News;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;

namespace LocalAIAgent.Tests.UnitTests;

public class FeedValidatorTests
{
    private const string SampleRss =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<rss version=\"2.0\"><channel><title>x</title><link>https://example.com</link><description>x</description>" +
        "<item><title>headline</title><link>https://example.com/a</link><description>summary</description></item>" +
        "</channel></rss>";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    private static FeedValidator BuildSut(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        StubHandler handler = new(respond);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });
        return new FeedValidator(factory.Object, NullLogger<FeedValidator>.Instance);
    }

    [Fact]
    public async Task ValidateAsync_AllUrlsRespondWithValidRss_AllResultsValid()
    {
        FeedValidator sut = BuildSut(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") });

        List<FeedUrlValidationResult> results = await sut.ValidateAsync(
            ["https://example.com/a.rss", "https://example.com/b.rss"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsValid));
        Assert.All(results, r => Assert.Null(r.ErrorMessage));
    }

    [Fact]
    public async Task ValidateAsync_NonAbsoluteUrl_RejectedWithoutHttpCall()
    {
        bool httpCalled = false;
        FeedValidator sut = BuildSut(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        List<FeedUrlValidationResult> results = await sut.ValidateAsync(["not-a-url"], TestContext.Current.CancellationToken);

        FeedUrlValidationResult only = Assert.Single(results);
        Assert.False(only.IsValid);
        Assert.NotNull(only.ErrorMessage);
        Assert.False(httpCalled);
    }

    [Fact]
    public async Task ValidateAsync_HttpErrorStatus_ReturnsInvalidWithStatusCodeInMessage()
    {
        FeedValidator sut = BuildSut(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        List<FeedUrlValidationResult> results = await sut.ValidateAsync(["https://example.com/missing.rss"], TestContext.Current.CancellationToken);

        FeedUrlValidationResult only = Assert.Single(results);
        Assert.False(only.IsValid);
        Assert.Contains("404", only.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_MalformedRss_ReturnsInvalid()
    {
        FeedValidator sut = BuildSut(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not actually xml", System.Text.Encoding.UTF8, "text/plain") });

        List<FeedUrlValidationResult> results = await sut.ValidateAsync(["https://example.com/bad.rss"], TestContext.Current.CancellationToken);

        FeedUrlValidationResult only = Assert.Single(results);
        Assert.False(only.IsValid);
        Assert.NotNull(only.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_MixedSuccessAndFailure_ReportsEachIndividually()
    {
        FeedValidator sut = BuildSut(req =>
            req.RequestUri!.ToString().Contains("good", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRss, System.Text.Encoding.UTF8, "application/xml") }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        List<FeedUrlValidationResult> results = await sut.ValidateAsync(
            ["https://example.com/good.rss", "https://example.com/bad.rss"],
            TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.True(results.Single(r => r.Url.Contains("good")).IsValid);
        Assert.False(results.Single(r => r.Url.Contains("bad")).IsValid);
    }
}
