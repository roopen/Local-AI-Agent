using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Mapping;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace LocalAIAgent.API.Application.UseCases;

public interface IGetDatasetUseCase
{
    Task<byte[]> GetDatasetZipAsync(CancellationToken cancellationToken = default);
}

internal sealed class GetDatasetUseCase(
    UserContext userContext,
    IGetTranslationUseCase translationUseCase) : IGetDatasetUseCase
{
    public async Task<byte[]> GetDatasetZipAsync(CancellationToken cancellationToken = default)
    {
        // Translation dataset — count batches first to balance with news entries
        List<ArticleTranslation> allTranslations = await userContext.ArticleTranslations
            .Where(t => t.OriginalTitle != null && t.OriginalSummary != null)
            .OrderBy(t => t.TargetLanguage)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        int translationBatchCount = 0;
        List<string> translationEntries = [];

        foreach (IGrouping<string, ArticleTranslation> group in allTranslations.GroupBy(t => t.TargetLanguage))
        {
            string translationSystemPrompt = translationUseCase.GetSystemPrompt(group.Key);
            List<ArticleTranslation> translations = [.. group];

            for (int i = 0; i < translations.Count; i += 3)
            {
                ArticleTranslation[] batch = translations.Skip(i).Take(3).ToArray();

                string userContent = $"Translate this JSON array to {group.Key}. Maintain the JSON structure perfectly:\n" +
                    JsonSerializer.Serialize(batch.Select(t => new { title = t.OriginalTitle, summary = t.OriginalSummary }));

                string assistantContent = JsonSerializer.Serialize(
                    batch.Select(t => new { title = t.TranslatedTitle, summary = t.TranslatedSummary }));

                translationEntries.Add(BuildEntry(translationSystemPrompt, userContent, assistantContent));
                translationBatchCount++;
            }
        }

        // Load all preferences and translation mapping
        List<UserPreferences> allPreferences = await userContext.UserPreferences
            .Include(p => p.EvaluationEntries)
            .Where(p => p.EvaluationEntries.Count > 0)
            .ToListAsync(cancellationToken);

        Dictionary<string, ArticleTranslation> translationsByLink = await userContext.ArticleTranslations
            .Where(t => t.OriginalTitle != null && t.OriginalSummary != null)
            .GroupBy(t => t.ArticleLink)
            .Select(g => g.OrderByDescending(t => t.CreatedAt).First())
            .ToDictionaryAsync(t => t.ArticleLink, cancellationToken);

        // Generate news entries balanced to match translation count; use all entries if there are no translations
        int newsTarget = translationBatchCount > 0 ? translationBatchCount : int.MaxValue;
        List<string> newsEntries = GetBalancedNewsEntries(
            allPreferences,
            translationsByLink,
            newsTarget);

        // Combine and shuffle both datasets
        List<string> allEntries = [.. newsEntries, .. translationEntries];
        allEntries = allEntries.OrderBy(_ => Random.Shared.Next()).ToList();

        // Smart split: use translation proportion to size the evaluation set (clamped 15–30%)
        int totalCount = allEntries.Count;
        double evalFraction = totalCount > 0
            ? Math.Clamp((double)translationBatchCount / totalCount, 0.15, 0.30)
            : 0.20;
        int evalCount = Math.Max(1, (int)Math.Round(totalCount * evalFraction));

        List<string> evalEntries = allEntries.Take(evalCount).ToList();
        List<string> trainEntries = allEntries.Skip(evalCount).ToList();

        using MemoryStream zipStream = new();
        using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "training_dataset.jsonl", trainEntries);
            WriteZipEntry(archive, "evaluation_dataset.jsonl", evalEntries);
        }

        return zipStream.ToArray();
    }

    private static List<string> GetBalancedNewsEntries(
        List<UserPreferences> allPreferences,
        Dictionary<string, ArticleTranslation> translationsByLink,
        int targetCount)
    {
        // Collect all evaluation entries from all users, deduplicated by entry Id
        Dictionary<int, (UserPreferences preferences, NewsEvaluationEntry entry)> seenIds = [];

        foreach (UserPreferences preferences in allPreferences)
        {
            foreach (NewsEvaluationEntry entry in preferences.EvaluationEntries)
            {
                seenIds.TryAdd(entry.Id, (preferences, entry));
            }
        }

        // Shuffle using a local Random instance to avoid tie collisions from Random.Shared
        Random rng = new();
        IEnumerable<(UserPreferences preferences, NewsEvaluationEntry entry)> query = seenIds.Values
            .OrderBy(_ => rng.Next());

        // When targetCount is bounded, take 3× to ensure enough entries to fill requested batches
        if (targetCount < int.MaxValue / 3)
            query = query.Take(targetCount * 3);

        List<(UserPreferences preferences, NewsEvaluationEntry entry)> shuffled = query.ToList();

        // Group selected entries back by user to preserve per-user system prompt context
        Dictionary<int, List<NewsEvaluationEntry>> entriesByUser = shuffled
            .GroupBy(pair => pair.preferences.Id)
            .ToDictionary(
                g => g.Key,
                g => g.Select(pair => pair.entry).ToList());

        List<string> newsEntries = [];
        int entriesGenerated = 0;

        foreach (UserPreferences preferences in allPreferences)
        {
            if (entriesGenerated >= targetCount)
                break;

            if (!entriesByUser.TryGetValue(preferences.Id, out List<NewsEvaluationEntry>? selectedEntries))
                continue;

            Domain.UserPreferences userPreferences = preferences.MapToDomainUserPreferences();
            string systemPrompt = userPreferences.BuildSystemPrompt();

            HashSet<string> knownTopics = new(StringComparer.OrdinalIgnoreCase);

            int offset = 0;
            while (offset < selectedEntries.Count && entriesGenerated < targetCount)
            {
                int batchSize = rng.Next(1, 4);
                NewsEvaluationEntry[] batch = selectedEntries.Skip(offset).Take(batchSize).ToArray();
                offset += batchSize;

                if (batch.Length == 0)
                    break;

                string topicsContext = EvaluateNewsUseCase.FormatKnownTopics(knownTopics);

                string userContent = topicsContext + string.Join("\n---ARTICLE SEPARATOR---\n",
                    batch.Select((e, i) =>
                    {
                        string source = Uri.TryCreate(e.ArticleLink, UriKind.Absolute, out Uri? uri)
                            ? uri.DnsSafeHost
                            : e.ArticleLink;
                        string title = translationsByLink.TryGetValue(e.ArticleLink, out ArticleTranslation? t)
                            ? t.OriginalTitle : e.ArticleTitle;
                        string summary = t is not null ? t.OriginalSummary : e.ArticleSummary;
                        return $"Article {i}:\n{title}\n\n{summary}\nSource: {source}\n";
                    }));

                foreach (NewsEvaluationEntry e in batch)
                    if (!string.IsNullOrWhiteSpace(e.ArticleTopic))
                        knownTopics.Add(e.ArticleTopic.Trim());

                string thinkBlock = BuildEvaluationThinkBlock(batch);
                string assistantContent = thinkBlock + JsonSerializer.Serialize(
                    batch.Select((e, i) => new
                    {
                        ArticleIndex = i,
                        e.Relevancy,
                        Topic = e.ArticleTopic ?? string.Empty,
                    }));

                newsEntries.Add(BuildEntry(systemPrompt, userContent, assistantContent));
                entriesGenerated++;
            }
        }

        return newsEntries;
    }

    private static string BuildEntry(string systemPrompt, string userContent, string assistantContent)
    {
        string messagesJson = JsonSerializer.Serialize(new { role = "system", content = systemPrompt }) + "," +
                              JsonSerializer.Serialize(new { role = "user", content = userContent }) + "," +
                              JsonSerializer.Serialize(new { role = "assistant", content = assistantContent });
        return "{\"messages\":[" + messagesJson + "]}";
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, List<string> entries)
    {
        ZipArchiveEntry zipEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using StreamWriter writer = new(zipEntry.Open(), Encoding.UTF8);
        foreach (string line in entries)
            writer.WriteLine(line);
    }

    private static string BuildEvaluationThinkBlock(NewsEvaluationEntry[] batch)
    {
        StringBuilder sb = new();
        for (int i = 0; i < batch.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(batch[i].Reasoning))
                sb.AppendLine($"Article {i}: {batch[i].Reasoning}");
        }

        if (sb.Length == 0)
            return string.Empty;

        return $"<|think|>\n{sb}<|end|>\n";
    }
}
