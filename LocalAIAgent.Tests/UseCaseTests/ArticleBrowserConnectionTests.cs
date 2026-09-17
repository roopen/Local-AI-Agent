using LocalAIAgent.Application.News.Reader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalAIAgent.Tests.UseCaseTests;

public class ArticleBrowserConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid endpoint")]
    public async Task MissingOrInvalidConfigurationReportsSetupError(string? endpoint)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ArticleReader:McpEndpoint"] = endpoint }).Build();
        PlaywrightArticleBrowserFactory factory = new(configuration, NullLogger<PlaywrightArticleBrowserFactory>.Instance);
        ArticleReaderUnavailableException failure = await Assert.ThrowsAsync<ArticleReaderUnavailableException>(() =>
            factory.OpenAsync(TestContext.Current.CancellationToken));
        Assert.Equal("article_browser_not_configured", failure.Code);
    }
}
