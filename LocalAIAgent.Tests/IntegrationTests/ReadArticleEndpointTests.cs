using System.Net;
using System.Net.Http.Json;
using LocalAIAgent.Application.News.Reader;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.Configuration;

namespace LocalAIAgent.Tests.IntegrationTests;

public sealed class ReadArticleEndpointTests
{
    public static bool McpEnabled => Environment.GetEnvironmentVariable("ARTICLE_READER_MCP_TEST_ENDPOINT") is not null;

    [Fact(SkipUnless = nameof(McpEnabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT for the full HTTP-to-browser smoke test.")]
    public async Task ActualHttpEndpointFetchesArticleThroughConfiguredBrowserService()
    {
        using CustomWebApplicationFactory factory = new();
        FakeChatClient chat = new();
        chat.EnqueueResponseText("No additional article content is available.");
        chat.EnqueueResponseText("{\"language\":\"en\"}");
        using WebApplicationFactory<API.Program> app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArticleReader:McpEndpoint"] = Environment.GetEnvironmentVariable("ARTICLE_READER_MCP_TEST_ENDPOINT"),
            }));
            builder.ConfigureServices(services => services.AddSingleton<ILlmRuntimeManager>(new FakeLlmRuntimeManager(
                new AIOptions { ModelId = "smoke-test", EndpointUrl = "http://unused-model/v1" }, chat)));
        });
        using HttpClient client = await CreateClientAsync(app);
        using (IServiceScope scope = app.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();
            db.Users.Add(new User { Id = 1, Username = "reader", Fido2Id = [1],
                Preferences = new() { TargetLanguage = "en", Prompt = "p", Interests = [], Dislikes = [] } });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/News/ReadArticle",
            new { url = "https://example.com/" }, TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        ReadArticleResult? result = await response.Content.ReadFromJsonAsync<ReadArticleResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("Example Domain", result.Original.Title);
        Assert.NotEmpty(result.Original.Markdown);
        Assert.Equal("notNeeded", result.TranslationStatus);
    }

    [Fact]
    public void ReaderResolvesThroughApplicationServices()
    {
        using CustomWebApplicationFactory factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReadArticleUseCase>());
    }

    [Fact]
    public async Task ValidRequestReachesControllerThroughMvcPipeline()
    {
        using CustomWebApplicationFactory factory = new();
        using HttpClient client = await CreateClientAsync(factory);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/News/ReadArticle",
            new { url = "https://example.com/article" }, TestContext.Current.CancellationToken);
        // This unseeded test database has no preferences; binding must reach that explicit 404.
        Assert.True(response.StatusCode == HttpStatusCode.NotFound,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidRequestInvokesReaderAndReturnsArticle()
    {
        using CustomWebApplicationFactory factory = new();
        Mock<IReadArticleUseCase> reader = new(MockBehavior.Strict);
        ReadArticleResult expected = new()
        {
            Original = new() { Title = "Fetched article", Markdown = "Full public text", SourceUrl = "https://example.com/article", Status = "complete" },
            TargetLanguage = "fi", TranslationStatus = "notNeeded",
        };
        using WebApplicationFactory<API.Program> app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(reader.Object)));
        using HttpClient client = await CreateClientAsync(app);
        using (IServiceScope scope = app.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();
            User user = new() { Id = 1, Username = "reader", Fido2Id = [1],
                Preferences = new() { TargetLanguage = "fi", Prompt = "p", Interests = [], Dislikes = [] } };
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            reader.Setup(r => r.ReadAsync(new ReadArticleRequest("https://example.com/article", "fi"),
                user.Id, user.Preferences.Id, "fi", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        }
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/News/ReadArticle",
            new { url = "https://example.com/article", sourceLanguage = "fi" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<ReadArticleResult>(TestContext.Current.CancellationToken));
        reader.VerifyAll();
    }

    public static TheoryData<object> InvalidRequests => new()
    {
        new { }, new { url = (string?)null }, new { url = "" },
        new { url = "https://example.com/" + new string('x', 4096) },
        new { url = "https://example.com/article", sourceLanguage = new string('x', 36) },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProgressIsFlushedBeforeCompletionAndEndsWithResultOrSafeError(bool fail)
    {
        using CustomWebApplicationFactory factory = new();
        Mock<IReadArticleUseCase> reader = new(MockBehavior.Strict);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Setup(r => r.ReadWithProgressAsync(It.IsAny<ReadArticleRequest>(), 1, It.IsAny<int>(), "fi",
            It.IsAny<Func<ArticleReaderProgress, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ReadArticleRequest request, int userId, int preferencesId, string target,
                Func<ArticleReaderProgress, Task> progress, CancellationToken ct) =>
            {
                await progress(new("waiting"));
                await release.Task.WaitAsync(ct);
                if (fail) throw new ArticleReaderUnavailableException("article_browser_unreachable", "Browser unavailable",
                    "HttpRequestException: Connection refused at http://user:password@localhost:8931/mcp?token=secret-value\nAuthorization: Bearer secret-value");
                await progress(new("translating", 1, 2));
                return new ReadArticleResult { Original = new() { Title = "Finished" }, TargetLanguage = target };
            });
        using WebApplicationFactory<API.Program> app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(reader.Object)));
        using HttpClient client = await CreateClientAsync(app);
        using (IServiceScope scope = app.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();
            db.Users.Add(new User { Id = 1, Username = "reader", Fido2Id = [1],
                Preferences = new() { TargetLanguage = "fi", Prompt = "p", Interests = [], Dislikes = [] } });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/News/ReadArticle")
        {
            Content = JsonContent.Create(new { url = "https://example.com/article" }),
        };
        request.Headers.Accept.ParseAdd("application/x-ndjson");
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
            using StreamReader lines = new(await response.Content.ReadAsStreamAsync(timeout.Token));
            Assert.Contains("\"phase\":\"waiting\"", await lines.ReadLineAsync(timeout.Token));
            release.TrySetResult();
            string tail = await lines.ReadToEndAsync(timeout.Token);
            Assert.Contains(fail ? "\"type\":\"error\"" : "\"type\":\"result\"", tail);
            Assert.Contains(fail ? "article_browser_unreachable" : "\"completed\":1", tail);
            if (fail)
            {
                Assert.Contains("Connection refused", tail);
                Assert.Contains("localhost:8931/mcp", tail);
                Assert.Contains("\"phase\":\"waiting\"", tail);
                Assert.Contains("\"requestId\":", tail);
                Assert.DoesNotContain("secret-value", tail);
                Assert.DoesNotContain("user:password", tail);
            }
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsReturn400InsteadOfThrowingDuringValidation(object request)
    {
        using CustomWebApplicationFactory factory = new();
        using HttpClient client = await CreateClientAsync(factory);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/News/ReadArticle", request,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpClient> CreateClientAsync(WebApplicationFactory<API.Program> factory)
    {
        HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage csrf = await client.GetAsync("/api/auth/csrf", TestContext.Current.CancellationToken);
        csrf.EnsureSuccessStatusCode();
        string tokenCookie = csrf.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(tokenCookie.Split(';')[0]["XSRF-TOKEN=".Length..]));
        return client;
    }
}
