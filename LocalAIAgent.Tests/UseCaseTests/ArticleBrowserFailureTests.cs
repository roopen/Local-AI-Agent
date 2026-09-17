using LocalAIAgent.Application.News.Reader;

namespace LocalAIAgent.Tests.UseCaseTests;

public class ArticleBrowserFailureTests
{
    [Theory]
    [InlineData("Timeout 30000ms exceeded", "article_page_timeout")]
    [InlineData("net::ERR_NAME_NOT_RESOLVED", "article_host_not_found")]
    [InlineData("net::ERR_CERT_AUTHORITY_INVALID", "article_certificate_error")]
    [InlineData("net::ERR_TUNNEL_CONNECTION_FAILED", "article_proxy_failed")]
    [InlineData("net::ERR_CONNECTION_REFUSED", "article_connection_failed")]
    [InlineData("Browser closed unexpectedly", "article_navigation_failed")]
    public void ToolFailureKeepsDiagnosticButOmitsSnapshot(string detail, string code)
    {
        ArticleReaderUnavailableException failure = ArticleBrowserFailure.FromToolError("browser_navigate",
            $"### Error\n{detail}\n### Page snapshot\nArticle body must not be included");
        Assert.Equal(code, failure.Code);
        Assert.Contains(detail, failure.Diagnostic);
        Assert.Contains("Tool: browser_navigate", failure.Diagnostic);
        Assert.DoesNotContain("Article body", failure.Diagnostic);
    }
}
