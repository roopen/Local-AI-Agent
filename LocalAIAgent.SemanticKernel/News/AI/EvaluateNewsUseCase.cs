using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface IEvaluateNewsUseCase
    {
        Task<List<NewsItem>> EvaluateArticles(List<NewsItem> articles, UserPreferences userPreferences);
        Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences);
    }

    internal class EvaluateNewsUseCase(
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
        Kernel kernel,
        AIOptions options,
        ILogger<EvaluateNewsUseCase> logger) : IEvaluateNewsUseCase
    {
        private readonly ChatHistory chatHistory = [];

        public async Task<List<NewsItem>> EvaluateArticles(List<NewsItem> articles, UserPreferences userPreferences)
        {
            string prompt = "Evaluate the following news article and return true if you think user wants to see it and " +
                "false if you think they don't want to see it. So the only allowed responses are the single word 'true' or 'false'." +
                " User preferences are as follows: ";
            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt + "User's dislikes: \n" + userPreferences.GetUserDislikesAsString() + "\n" +
                "User's likes: \n" + userPreferences.GetUserInterestsAsString(), allowFunctionUse: false);

            List<NewsItem> result = [];

            foreach (NewsItem article in articles)
            {
                // clear chat history to keep context small and focused on only the current article
                chatHistory.Clear();

                if (string.IsNullOrWhiteSpace(article.Content))
                    continue;

                chatHistory.AddUserMessage(article.Content);
                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                                    chatHistory,
                                    openAiSettings,
                                    kernel)
                                    .ConfigureAwait(false))
                {
                    if (string.IsNullOrEmpty(content.Content))
                        continue;

                    bool? isRelevantArticle = content.Content?.Trim().ToLowerInvariant() switch
                    {
                        "true" => true,
                        "false" => false,
                        _ => null
                    };

                    if (isRelevantArticle.HasValue && isRelevantArticle.Value)
                        result.Add(article);
                }
            }

            double filterPercentage = 100 - (result.Count / (double)articles.Count * 100);
            NewsLogging.LogNewsFiltered(logger, articles.Count, result.Count, filterPercentage, null);

            return result;
        }

        public async Task<EvaluatedNewsArticles> EvaluateArticlesV2(List<NewsItem> articles, UserPreferences userPreferences)
        {
            const int BatchSize = 5;
            string prompt = "Evaluate the following news articles based on user preferences. " +
                "Respond with an array of evaluations using the following json structure and nothing else." +
                "[{" +
                "\"ArticleIndex\": 0," +
                "\"Relevancy\": \"High|Medium|Low\"," +
                "}]\n" +
                "User preferences are as follows: ";

            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt + "User's dislikes: \n" + userPreferences.GetUserDislikesAsString() + "\n" +
                "User's likes: \n" + userPreferences.GetUserInterestsAsString(), allowFunctionUse: false);

            List<NewsArticle> result = [];
            IEnumerable<NewsItem[]> articleBatches = articles.Where(a => !string.IsNullOrWhiteSpace(a.Content))
                                        .Chunk(BatchSize);

            foreach (NewsItem[] batch in articleBatches)
            {
                chatHistory.Clear();
                string batchContent = string.Join("\n---ARTICLE SEPARATOR---\n",
                    batch.Select((a, i) => $"Article {i}:\n{a.Content}"));

                chatHistory.AddUserMessage(batchContent);
                string jsonContent = string.Empty;

                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                                    chatHistory,
                                    openAiSettings,
                                    kernel)
                                    .ConfigureAwait(false))
                {
                    if (string.IsNullOrEmpty(content.Content))
                        continue;

                    jsonContent += content.Content;

                    if (jsonContent.StartsWith("```", StringComparison.InvariantCultureIgnoreCase) &&
                        jsonContent.EndsWith("```", StringComparison.InvariantCultureIgnoreCase) &&
                        jsonContent.Length > 3)
                    {
                        try
                        {
                            List<EvaluationResult> evaluations = EvaluationResult.Deserialize(jsonContent);

                            if (evaluations != null)
                            {
                                AddResults(result, batch, evaluations);
                                jsonContent = string.Empty;
                            }
                        }
                        catch
                        {
                            // Continue processing if batch parsing fails
                            continue;
                        }
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
                        Reasoning = evaluations[i].Reasoning
                    };
                    result.Add(newsArticle);
                }
            }
        }
    }
}
