using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using Polly;
using Polly.Retry;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface IEvaluateNewsUseCase
    {
        Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences, bool includeReasoning = false);
    }

    public class EvaluateNewsUseCase(
        Kernel kernel,
        AIOptions options,
        IMemoryCache memoryCache,
        INewsDatasetRepository newsDatasetRepository,
        ILogger<EvaluateNewsUseCase> logger) : IEvaluateNewsUseCase
    {

        public async Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences, bool includeReasoning = false)
        {
            List<NewsArticle> result = await EvaluateCoreAsync(articles, userPreferences, "General", includeReasoning);

            double filterPercentage = 100 - (result.Count / (double)articles.Count * 100);
            NewsLogging.LogNewsFiltered(logger, articles.Count, result.Count, filterPercentage, null);

            return new EvaluatedNewsArticles { NewsArticles = result, LowRelevancyPercentage = (decimal)filterPercentage };
        }

        private async Task<List<NewsArticle>> EvaluateCoreAsync(
            List<NewsItem> articles,
            UserPreferences userPreferences,
            string serviceId,
            bool includeReasoning = false)
        {
            const int BatchSize = 3;

            ChatCompletionAgent agent = new()
            {
                Instructions = userPreferences.BuildSystemPrompt(),
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false, serviceId: serviceId)),
            };

            List<NewsArticle> result = [];
            IEnumerable<NewsItem[]> articleBatches = articles.Where(a => !string.IsNullOrWhiteSpace(a.Content))
                                        .Chunk(BatchSize);

            string preferencesKey = GetPreferencesKey(userPreferences) + "_" + serviceId;

            foreach (NewsItem[] batch in articleBatches)
            {
                IEnumerable<string> batchLinks = batch.Select(a => a.Link ?? string.Empty).Where(l => l.Length > 0);
                Dictionary<string, CachedNewsEvaluation> cached = await newsDatasetRepository.GetCachedEvaluationsAsync(batchLinks, CancellationToken.None);

                foreach (NewsItem item in batch.Where(a => a.Link != null && cached.ContainsKey(a.Link)))
                {
                    CachedNewsEvaluation entry = cached[item.Link!];
                    result.Add(new NewsArticle
                    {
                        Title = item.Title,
                        Summary = item.Summary,
                        PublishedDate = item.PublishDate.DateTime,
                        Link = item.Link!,
                        Source = item.Source ?? string.Empty,
                        Categories = [],
                        Relevancy = entry.Relevancy,
                        Topic = entry.Topic,
                        Reasoning = includeReasoning ? entry.Reasoning : null,
                        InputTokens = null,
                        OutputTokens = null,
                    });
                }

                NewsItem[] uncachedBatch = batch.Where(a => a.Link == null || !cached.ContainsKey(a.Link)).ToArray();
                if (uncachedBatch.Length == 0)
                    continue;

                HashSet<string> knownTopics = LoadKnownTopics(preferencesKey);
                string topicsEventsContext = FormatKnownTopics(knownTopics);

                string batchContent = topicsEventsContext + string.Join("\n---ARTICLE SEPARATOR---\n",
                    uncachedBatch.Select((a, i) => $"Article {i}:\n{a.Content}\nSource: {a.Source}\n"));

                string jsonContent = string.Empty;

                using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));
                ChatMessageContent userMessage = new(AuthorRole.User, batchContent);
                List<StreamingChatMessageContent> stream = await GetStreamWithRetryAsync(agent, userMessage, cts.Token).ConfigureAwait(false);

                List<ChatTokenUsage> tokenUsageTotal = [];
                if (stream.Select(c => c.Metadata?.GetValueOrDefault("Usage")).LastOrDefault(u => u is not null) is ChatTokenUsage tokenUsage)
                {
                    tokenUsageTotal.Add(tokenUsage);
                }

                foreach (StreamingChatMessageContent? content in stream)
                {
                    if (string.IsNullOrEmpty(content.Content))
                        continue;

                    jsonContent += content.Content;
                }

                if (!string.IsNullOrWhiteSpace(jsonContent))
                {
                    try
                    {
                        List<EvaluationResult> evaluations = EvaluationResult.Deserialize(jsonContent);

                        if (evaluations != null)
                        {
                            if (tokenUsageTotal.Count > 0)
                            {
                                try
                                {
                                    foreach (EvaluationResult evalr in evaluations)
                                        foreach (ChatTokenUsage tu in tokenUsageTotal)
                                            evalr.TokenUsage = tu;
                                }
                                catch { }

                                int totalInputTokens = tokenUsageTotal.Sum(tu => tu.InputTokenCount);
                                int totalOutputTokens = tokenUsageTotal.Sum(tu => tu.OutputTokenCount);
                                int totalTokensUsed = totalInputTokens + totalOutputTokens;

                                NewsLogging.LogTokenUsage(logger, totalInputTokens, totalOutputTokens, totalTokensUsed, null);
                            }
                            UpdateKnownTopicsAndEvents(knownTopics, evaluations);
                            AddResults(result, uncachedBatch, evaluations, includeReasoning);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Failed to deserialize LLM response: " + jsonContent);
                    }
                }
            }

            await newsDatasetRepository.SaveAsync(result, userPreferences.Id, options.UseResultsForDataset, options.ModelId, CancellationToken.None);

            return result;
        }

        private const string TopicsCacheKeyPrefix = "news_known_topics_";

        private static string GetPreferencesKey(UserPreferences preferences) =>
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(preferences.Prompt + string.Join(",", preferences.Interests))));

        private HashSet<string> LoadKnownTopics(string key)
        {
            HashSet<string> topics = memoryCache.GetOrCreate(TopicsCacheKeyPrefix + key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            })!;

            topics.RemoveWhere(t => t.Contains('/'));

            return topics;
        }

        private static void UpdateKnownTopicsAndEvents(HashSet<string> topics, IEnumerable<EvaluationResult> evaluations)
        {
            foreach (EvaluationResult evalr in evaluations)
            {
                if (!string.IsNullOrWhiteSpace(evalr.Topic))
                {
                    string topic = NormalizeLabel(evalr.Topic);
                    if (!string.IsNullOrWhiteSpace(topic) && !IsCompoundLabel(topic, topics))
                        topics.Add(topic);
                }
            }
        }

        private static bool IsCompoundLabel(string label, HashSet<string> existing) =>
            label.Contains('/') && label.Split('/').Select(p => p.Trim()).Any(existing.Contains);

        private static string NormalizeLabel(string label)
        {
            label = label.Trim();
            int start = 0;
            while (start < label.Length && !char.IsAsciiLetter(label[start]))
                start++;
            return start > 0 ? label[start..] : label;
        }

        public static string FormatKnownTopics(HashSet<string> topics)
        {
            if (topics.Count == 0)
                return "Topic Rule: Assign a single, broad category (e.g., Finance, Tech, Politics). No slashes.\n";

            StringBuilder sb = new();
            sb.AppendLine("### Topic Categorization Rules");
            sb.AppendLine("1. **Subject vs. Cause Rule**: Categorize by the *Main Subject*, not the *Cause*.");
            sb.AppendLine("   - If the subject is Stocks, Inflation, or Markets, use **Finance**, even if the cause is a war or treaty.");
            sb.AppendLine("   - If the subject is a Missile Strike, Diplomatic Meeting, or Treaty, use **Geopolitics**.");

            sb.AppendLine("2. **Topic Definitions**:");
            sb.AppendLine("   - **Finance**: Stock prices, indices (S&P 500), inflation (CPI/Wholesale), central banks, oil *prices*.");
            sb.AppendLine("   - **Geopolitics**: Military actions, international sanctions, state-level negotiations, borders.");

            if (topics.Count > 0)
            {
                sb.AppendLine("\n[Known Topics]:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {string.Join(", ", topics.OrderBy(x => x))}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AddResults(List<NewsArticle> result, NewsItem[] batch, List<EvaluationResult> evaluations, bool includeReasoning = false)
        {
            for (int i = 0; i < evaluations.Count; i++)
            {
                NewsArticle newsArticle = new()
                {
                    Title = batch[i].Title,
                    Summary = batch[i].Summary,
                    PublishedDate = batch[i].PublishDate.DateTime,
                    Link = batch[i].Link ?? string.Empty,
                    Source = batch[i].Source ?? string.Empty,
                    Categories = [],
                    Relevancy = evaluations[i].Relevancy,
#if DEBUG
                    Reasoning = evaluations[i].Reasoning,
#else
                    Reasoning = includeReasoning ? evaluations[i].Reasoning : null,
#endif
                    Topic = evaluations[i].Topic,
                    InputTokens = evaluations[i] == evaluations.Where(ev => ev.Relevancy is Relevancy.High).LastOrDefault()
                        ? evaluations[i].TokenUsage?.InputTokenCount : null,
                    OutputTokens = evaluations[i] == evaluations.Where(ev => ev.Relevancy is Relevancy.High).LastOrDefault()
                        ? evaluations[i].TokenUsage?.OutputTokenCount : null
                };
                result.Add(newsArticle);
            }
        }

        private static async Task<List<StreamingChatMessageContent>> GetStreamWithRetryAsync(
            ChatCompletionAgent agent,
            ChatMessageContent message,
            CancellationToken cancellationToken)
        {
            AsyncRetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(20, attempt)));

            return await retryPolicy.ExecuteAsync(async ct =>
            {
                List<StreamingChatMessageContent> chunks = [];
                ChatHistoryAgentThread thread = new();
                await foreach (StreamingChatMessageContent? content in agent.InvokeStreamingAsync(message, thread, cancellationToken: ct)
                                    .ConfigureAwait(false))
                {
                    if (content is null)
                        continue;
                    chunks.Add(content);
                }
                return chunks;
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
