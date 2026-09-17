namespace LocalAIAgent.Application.News.Reader;

// Tool diagnostics are untrusted and can include page content or URLs. Only expose known codes/messages.
internal static class ArticleBrowserFailure
{
    internal static ArticleReaderUnavailableException FromToolError(string tool, string diagnostic)
    {
        ArticleReaderUnavailableException failure = Classify(tool, diagnostic);
        // The Error section precedes any page snapshot. Never include the snapshot in diagnostics.
        string error = diagnostic.Trim();
        if (error.StartsWith("### Error", StringComparison.Ordinal)) error = error[9..].TrimStart();
        int nextSection = error.IndexOf("\n### ", StringComparison.Ordinal);
        if (nextSection >= 0) error = error[..nextSection];
        return new(failure.Code, failure.Message, $"Tool: {tool}\n{error}");
    }

    private static ArticleReaderUnavailableException Classify(string tool, string diagnostic)
    {
        if (diagnostic.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Contains("ERR_TIMED_OUT", StringComparison.OrdinalIgnoreCase))
            return new("article_page_timeout", "The publisher's page took too long to load. Retry or open the publisher's site.");
        if (diagnostic.Contains("ERR_NAME_NOT_RESOLVED", StringComparison.Ordinal))
            return new("article_host_not_found", "The publisher's address could not be resolved. Check the article link or retry later.");
        if (diagnostic.Contains("ERR_CERT_", StringComparison.Ordinal)
            || diagnostic.Contains("ERR_SSL_", StringComparison.Ordinal))
            return new("article_certificate_error", "A secure connection to the publisher could not be established.");
        if (diagnostic.Contains("ERR_TUNNEL_CONNECTION_FAILED", StringComparison.Ordinal)
            || diagnostic.Contains("ERR_PROXY_CONNECTION_FAILED", StringComparison.Ordinal))
            return new("article_proxy_failed", "The article browser's network proxy could not reach this page.");
        if (diagnostic.Contains("ERR_CONNECTION_", StringComparison.Ordinal))
            return new("article_connection_failed", "The connection to the publisher failed. Retry or open the publisher's site.");
        return tool == "browser_navigate"
            ? new("article_navigation_failed", "The article browser could not open the publisher's page.")
            : new("article_extraction_failed", "The article browser could not retrieve readable text from this page.");
    }
}
