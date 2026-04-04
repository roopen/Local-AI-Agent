using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface IEvaluateNewsUseCase
    {
        Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences);
    }

    internal class EvaluateNewsUseCase(
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
        Kernel kernel,
        AIOptions options,
        IMemoryCache memoryCache,
        ILogger<EvaluateNewsUseCase> logger) : IEvaluateNewsUseCase
    {
        private readonly ChatHistory chatHistory = [];

        public async Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences)
        {
            const int BatchSize = 5;
            string feedbackSection = userPreferences.FeedbackExamples.Count > 0
                ? "\nPast feedback from this user (use these as additional examples when judging relevancy):\n" +
                  userPreferences.GetFeedbackExamplesAsString()
                : string.Empty;

            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                userPreferences.BuildSystemPrompt(),
                allowFunctionUse: false);

            List<NewsArticle> result = [];
            IEnumerable<NewsItem[]> articleBatches = articles.Where(a => !string.IsNullOrWhiteSpace(a.Content))
                                        .Chunk(BatchSize);

            foreach (NewsItem[] batch in articleBatches)
            {
                chatHistory.Clear();
                string batchContent = string.Join("\n---ARTICLE SEPARATOR---\n",
                    batch.Select((a, i) => $"Article {i}:\n{a.Content}\nSource: {a.Source}\n"));

                chatHistory.AddUserMessage(batchContent);
                string jsonContent = string.Empty;

                using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));
                List<StreamingChatMessageContent> stream = await GetStreamWithRetryAsync(chatHistory, openAiSettings, kernel, cts.Token).ConfigureAwait(false);

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
                            AddResults(result, batch, evaluations);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Failed to deserialize LLM response: " + jsonContent);
                    }
                }
            }

            double filterPercentage = 100 - (result.Count / (double)articles.Count * 100);
            NewsLogging.LogNewsFiltered(logger, articles.Count, result.Count, filterPercentage, null);

            return new EvaluatedNewsArticles { NewsArticles = result, LowRelevancyPercentage = (decimal)filterPercentage };
        }

        private static void AddResults(List<NewsArticle> result, NewsItem[] batch, List<EvaluationResult> evaluations)
        {
            for (int i = 0; i < evaluations.Count; i++)
            {
                if (evaluations[i].Relevancy is Relevancy.High)
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
                        Reasoning = evaluations[i].Reasoning,
                        InputTokens = evaluations[i] == evaluations.Where(ev => ev.Relevancy is Relevancy.High).Last()
                            ? evaluations[i].TokenUsage?.InputTokenCount : null,
                        OutputTokens = evaluations[i] == evaluations.Where(ev => ev.Relevancy is Relevancy.High).Last()
                            ? evaluations[i].TokenUsage?.OutputTokenCount : null
                    };
                    result.Add(newsArticle);
                }
            }
        }

        private async Task<List<StreamingChatMessageContent>> GetStreamWithRetryAsync(
            ChatHistory history,
            OpenAIPromptExecutionSettings settings,
            Kernel kernelInstance,
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
                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                                    history,
                                    settings,
                                    kernelInstance,
                                    ct)
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
