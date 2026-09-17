using System.ComponentModel.DataAnnotations;

namespace LocalAIAgent.Application.News.Reader;

// Use property binding so MVC and OpenAPI both see the same validation metadata.
// Property-targeted validation on a positional record throws before the action runs.
public sealed record ReadArticleRequest
{
    [Required, MaxLength(4096)]
    public string Url { get; init; } = "";

    [MaxLength(35)]
    public string? SourceLanguage { get; init; }

    public ReadArticleRequest() { }

    public ReadArticleRequest(string url, string? sourceLanguage = null)
    {
        Url = url;
        SourceLanguage = sourceLanguage;
    }
}

public sealed record ArticleContent
{
    public string Title { get; init; } = "";
    public string Markdown { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public string? Language { get; init; }
    public string? Author { get; init; }
    public string? PublishedAt { get; init; }
    public bool AccessRestricted { get; init; }
    // complete, partial, blocked, unavailable
    public string Status { get; init; } = "unavailable";
}

public sealed record ReadArticleResult
{
    public required ArticleContent Original { get; init; }
    public string? TranslatedMarkdown { get; init; }
    public string? TranslatedTitle { get; init; }
    public string? DetectedLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    // notNeeded, complete, failed, unavailable
    public string TranslationStatus { get; init; } = "unavailable";
    public string? Message { get; init; }
}

public sealed record ArticleReaderProgress(string Phase, int? Completed = null, int? Total = null);

public interface IReadArticleUseCase
{
    Task<ReadArticleResult> ReadAsync(ReadArticleRequest request, int userId,
        int preferencesId, string targetLanguage, CancellationToken cancellationToken);
    Task<ReadArticleResult> ReadWithProgressAsync(ReadArticleRequest request, int userId,
        int preferencesId, string targetLanguage, Func<ArticleReaderProgress, Task> progress, CancellationToken cancellationToken);
}

public interface IArticleBrowser : IAsyncDisposable
{
    Task NavigateAsync(string url, CancellationToken cancellationToken);
    Task<ArticleContent> ExtractAsync(CancellationToken cancellationToken);
    Task<string> SnapshotAsync(CancellationToken cancellationToken);
    Task<string> ClickAsync(string reference, string label, CancellationToken cancellationToken);
    Task WaitAsync(CancellationToken cancellationToken);
}

public interface IArticleBrowserFactory
{
    Task<IArticleBrowser> OpenAsync(CancellationToken cancellationToken);
}

public static class ArticleUrlPolicy
{
    public static bool IsValid(string? value) =>
        value is { Length: <= 4096 } && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo)
        && uri.Port is 80 or 443 && uri.HostNameType != UriHostNameType.Unknown
        && !uri.IsLoopback;
}
