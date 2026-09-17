using System.Diagnostics;
using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace LocalAIAgent.Application.News.Reader;

// Singleton owns only reader capacity/cache. Neither is shared with the news stream.
internal sealed class ArticleReaderResources : IDisposable
{
    public SemaphoreSlim Browsers { get; } = new(2, 2);
    public MemoryCache Cache { get; } = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    public void Dispose() { Browsers.Dispose(); Cache.Dispose(); }
}

internal sealed class ReadArticleUseCase(IArticleBrowserFactory browsers, ArticleReaderAgent agent,
    ArticleBodyTranslator translator, ILlmRuntimeManager runtimes, ArticleReaderResources resources,
    ILogger<ReadArticleUseCase> logger, IArticleReaderPersistentCache? persistentCache = null) : IReadArticleUseCase
{
    public Task<ReadArticleResult> ReadAsync(ReadArticleRequest request, int userId,
        int preferencesId, string targetLanguage, CancellationToken cancellationToken) =>
        ReadWithProgressAsync(request, userId, preferencesId, targetLanguage, _ => Task.CompletedTask, cancellationToken);

    public async Task<ReadArticleResult> ReadWithProgressAsync(ReadArticleRequest request, int userId,
        int preferencesId, string targetLanguage, Func<ArticleReaderProgress, Task> progress, CancellationToken cancellationToken)
    {
        if (!ArticleUrlPolicy.IsValid(request.Url)) throw new ArgumentException("A public HTTP(S) article URL is required.");
        string url = new UriBuilder(request.Url) { Fragment = "" }.Uri.AbsoluteUri;
        LlmRuntimeSnapshot runtime;
        try { runtime = runtimes.GetRequiredSnapshot(preferencesId); }
        catch (InvalidOperationException)
        {
            throw new ArticleReaderUnavailableException("article_model_unavailable", "Connect an AI model in settings before using the article reader.");
        }
        ArticleReaderCacheKey key = new(userId, url, targetLanguage, runtime.Options.EndpointUrl, runtime.Options.ModelId);
        if (resources.Cache.TryGetValue(key, out ReadArticleResult? cached) && cached is not null) return cached;
        if (persistentCache is not null)
        {
            CachedArticle? saved = await persistentCache.GetAsync(key, cancellationToken);
            if (saved is not null && saved.ExpiresAt > DateTimeOffset.UtcNow)
            {
                CacheInMemory(key, saved);
                return saved.Result;
            }
        }
        Stopwatch timer = Stopwatch.StartNew();
        ArticleContent original;
        using (CancellationTokenSource extraction = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            extraction.CancelAfter(TimeSpan.FromSeconds(120));
            await progress(new("waiting"));
            await resources.Browsers.WaitAsync(extraction.Token);
            try
            {
                await progress(new("opening"));
                await using IArticleBrowser browser = await browsers.OpenAsync(extraction.Token);
                original = await agent.ExtractAsync(browser, url, runtime, extraction.Token, progress);
            }
            finally { resources.Browsers.Release(); }
        }
        ReadArticleResult result = await translator.TranslateAsync(original, request.SourceLanguage, targetLanguage, runtime, cancellationToken, progress);
        if (original.Status == "complete" && result.TranslationStatus is "complete" or "notNeeded")
        {
            CachedArticle saved = new(result, DateTimeOffset.UtcNow.AddMinutes(30));
            CacheInMemory(key, saved);
            if (persistentCache is not null) await persistentCache.SetAsync(key, saved, cancellationToken);
        }
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Article reader completed in {ElapsedMs}ms: extraction {Extraction}, translation {Translation}",
                timer.ElapsedMilliseconds, original.Status, result.TranslationStatus);
        return result;
    }

    private void CacheInMemory(ArticleReaderCacheKey key, CachedArticle saved)
    {
        ReadArticleResult result = saved.Result;
        long size = 1024L + 2L * (result.Original.Markdown.Length + result.Original.Title.Length
            + (result.TranslatedMarkdown?.Length ?? 0) + (result.TranslatedTitle?.Length ?? 0));
        resources.Cache.Set(key, result, new MemoryCacheEntryOptions { AbsoluteExpiration = saved.ExpiresAt, Size = size });
    }
}
