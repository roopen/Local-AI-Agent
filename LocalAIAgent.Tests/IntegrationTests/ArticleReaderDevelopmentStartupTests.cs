using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LocalAIAgent.Tests.IntegrationTests;

public class ArticleReaderDevelopmentStartupTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("ARTICLE_READER_MCP_TEST_ENDPOINT") is not null;

    [Theory]
    [InlineData("http://localhost:8931/mcp", true)]
    [InlineData("http://127.0.0.1:8931/mcp", true)]
    [InlineData("http://article-mcp:8931/mcp", false)]
    [InlineData("http://localhost:8932/mcp", false)]
    [InlineData("http://localhost:8931/custom", false)]
    [InlineData("http://user:secret@localhost:8931/mcp", false)]
    [InlineData("not a URL", false)]
    public void OnlyTheDefaultLocalEndpointIsManaged(string endpoint, bool expected) =>
        Assert.Equal(expected, ArticleReaderDevelopmentStartup.IsManagedEndpoint(endpoint));

    [Fact(SkipUnless = nameof(Enabled), Skip = "Set ARTICLE_READER_MCP_TEST_ENDPOINT for local container startup verification.")]
    public async Task LocalStartupIsRepeatableAndChecksRealMcpAndChromium()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Local-AI-Agent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        Mock<IWebHostEnvironment> environment = new();
        environment.SetupGet(e => e.ContentRootPath).Returns(Path.Combine(directory.FullName, "LocalAIAgent.API"));
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArticleReader:McpEndpoint"] = Environment.GetEnvironmentVariable("ARTICLE_READER_MCP_TEST_ENDPOINT"),
        }).Build();
        ArticleReaderDevelopmentStartup startup = new(configuration, environment.Object, NullLogger<ArticleReaderDevelopmentStartup>.Instance);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await startup.EnsureReadyAsync(timeout.Token);
        await startup.EnsureReadyAsync(timeout.Token);
        await startup.StopAsync(timeout.Token);
    }
}
