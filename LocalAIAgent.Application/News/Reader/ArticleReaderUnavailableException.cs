namespace LocalAIAgent.Application.News.Reader;

// The public message is separate from diagnostics, which the API redacts before returning.
public sealed class ArticleReaderUnavailableException(string code, string message, string? diagnostic = null) : Exception(message)
{
    public string Code { get; } = code;
    public string? Diagnostic { get; } = diagnostic;
}
