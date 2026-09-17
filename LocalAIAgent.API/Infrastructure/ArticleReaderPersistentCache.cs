using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalAIAgent.Application.News.Reader;
using Microsoft.Data.Sqlite;

namespace LocalAIAgent.API.Infrastructure;

// Separate disposable cache database: article bodies never enter training/evaluation datasets.
internal sealed class ArticleReaderPersistentCache(string path, TimeProvider clock,
    ILogger<ArticleReaderPersistentCache> logger) : IArticleReaderPersistentCache
{
    private const long PerUserBytes = 16 * 1024 * 1024;
    private const long TotalBytes = 64 * 1024 * 1024;

    public async Task<CachedArticle?> GetAsync(ArticleReaderCacheKey key, CancellationToken cancellationToken)
    {
        try
        {
            using SqliteConnection connection = await OpenAsync(cancellationToken);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Payload, ExpiresAt FROM Articles WHERE CacheKey=$key AND UserId=$user AND ExpiresAt>$now";
            command.Parameters.AddWithValue("$key", Hash(key));
            command.Parameters.AddWithValue("$user", key.UserId);
            command.Parameters.AddWithValue("$now", clock.GetUtcNow().ToUnixTimeMilliseconds());
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            ReadArticleResult? result = JsonSerializer.Deserialize<ReadArticleResult>(reader.GetString(0));
            return IsComplete(result) && result!.TargetLanguage == key.TargetLanguage
                ? new(result, DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1))) : null;
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            logger.LogWarning("Persistent article cache read failed ({FailureType}); fetching normally.", ex.GetType().Name);
            return null;
        }
    }

    public async Task SetAsync(ArticleReaderCacheKey key, CachedArticle article, CancellationToken cancellationToken)
    {
        if (!IsComplete(article.Result) || article.Result.TargetLanguage != key.TargetLanguage || article.ExpiresAt <= clock.GetUtcNow()) return;
        string payload = JsonSerializer.Serialize(article.Result);
        long size = Encoding.UTF8.GetByteCount(payload);
        if (size > PerUserBytes) return;
        try
        {
            using SqliteConnection connection = await OpenAsync(cancellationToken);
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM Articles WHERE ExpiresAt <= $now;
                INSERT OR REPLACE INTO Articles(CacheKey, UserId, ExpiresAt, SizeBytes, Payload)
                VALUES($key, $user, $expires, $size, $payload);
                DELETE FROM Articles WHERE CacheKey IN (
                    SELECT CacheKey FROM (
                        SELECT CacheKey, SUM(SizeBytes) OVER (ORDER BY ExpiresAt DESC, CacheKey) AS Bytes,
                            ROW_NUMBER() OVER (ORDER BY ExpiresAt DESC, CacheKey) AS Position
                        FROM Articles WHERE UserId=$user
                    ) WHERE Bytes>$perUser OR Position>64
                );
                DELETE FROM Articles WHERE CacheKey IN (
                    SELECT CacheKey FROM (
                        SELECT CacheKey, SUM(SizeBytes) OVER (ORDER BY ExpiresAt DESC, CacheKey) AS Bytes,
                            ROW_NUMBER() OVER (ORDER BY ExpiresAt DESC, CacheKey) AS Position
                        FROM Articles
                    ) WHERE Bytes>$total OR Position>256
                );
                """;
            command.Parameters.AddWithValue("$now", clock.GetUtcNow().ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$key", Hash(key));
            command.Parameters.AddWithValue("$user", key.UserId);
            command.Parameters.AddWithValue("$expires", article.ExpiresAt.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$size", size);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$perUser", PerUserBytes);
            command.Parameters.AddWithValue("$total", TotalBytes);
            await command.ExecuteNonQueryAsync(cancellationToken);
            transaction.Commit();
        }
        catch (Exception ex) when (IsCacheFailure(ex))
        {
            logger.LogWarning("Persistent article cache write failed ({FailureType}); retaining the article in memory.", ex.GetType().Name);
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 2 }.ToString());
        try
        {
            await connection.OpenAsync(ct);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Articles(
                    CacheKey TEXT PRIMARY KEY, UserId INTEGER NOT NULL, ExpiresAt INTEGER NOT NULL,
                    SizeBytes INTEGER NOT NULL, Payload TEXT NOT NULL);
                """;
            await command.ExecuteNonQueryAsync(ct);
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static string Hash(ArticleReaderCacheKey key) =>
        // Invalidate results extracted before embedded publisher-promotion filtering.
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("reader-v5:" + JsonSerializer.Serialize(key))));

    private static bool IsComplete(ReadArticleResult? result) =>
        result?.Original is { Status: "complete" } original && !string.IsNullOrWhiteSpace(original.Markdown)
        && (result.TranslationStatus == "notNeeded" || result.TranslationStatus == "complete"
            && !string.IsNullOrWhiteSpace(result.TranslatedTitle) && !string.IsNullOrWhiteSpace(result.TranslatedMarkdown));

    private static bool IsCacheFailure(Exception ex) => ex is SqliteException or IOException or UnauthorizedAccessException or JsonException;
}
