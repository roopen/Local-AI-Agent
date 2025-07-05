using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
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
        IChatCompletionService chatCompletion,
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
            string prompt = "Evaluate the following news article and respond by using the following json structure and nothing else:\n" +
                "{" +
                "\"Relevancy\": \"High|Medium|Low\"," +
                "\"Categories\": [\"Politics\", \"EU\"]," +
                "\"Reasoning\": \"The part of the user preferences that you used\"," +
                "\"TranslatedTitle\": \"English translation. Null if original title is English.\"," +
                "\"TranslatedSummary\": \"English translation. Null if original summary is English.\"," +
                "}" +
                "\nUser preferences are as follows: ";
            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt + "User's dislikes: \n" + userPreferences.GetUserDislikesAsString() + "\n" +
                "User's likes: \n" + userPreferences.GetUserInterestsAsString(), allowFunctionUse: false);

            List<NewsArticle> result = [];
            List<EvaluationResult> evaluationResults = [];

            foreach (NewsItem article in articles)
            {
                // clear chat history to keep context small and focused on only the current article
                chatHistory.Clear();

                if (string.IsNullOrWhiteSpace(article.Content))
                    continue;

                chatHistory.AddUserMessage(article.Content);
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
                        if (TryConstructNewsArticle(article, jsonContent, out NewsArticle? newsArticle))
                        {
                            jsonContent = string.Empty;

                            if (newsArticle is not null && newsArticle.Relevancy is not Relevancy.Low)
                                result.Add(newsArticle);
                        }
                    }
                }
            }

            double filterPercentage = 100 - (result.Count / (double)articles.Count * 100);
            NewsLogging.LogNewsFiltered(logger, articles.Count, result.Count, filterPercentage, null);

            return new EvaluatedNewsArticles { NewsArticles = result, LowRelevancyPercentage = (decimal)filterPercentage };
        }

        private static bool TryConstructNewsArticle(NewsItem article, string aiResponse, out NewsArticle? newsArticle)
        {
            try
            {
                EvaluationResult evaluationResult = EvaluationResult.Deserialize(aiResponse);
                newsArticle = new NewsArticle
                {
                    Title = evaluationResult.TranslatedTitle ?? article.Title,
                    Summary = evaluationResult.TranslatedSummary ?? article.Summary,
                    PublishedDate = article.PublishDate.DateTime,
                    Link = article.Link ?? string.Empty,
                    Source = article.Source ?? string.Empty,
                    Categories = [.. article.Categories.Concat(evaluationResult.Categories)],
                    Relevancy = evaluationResult.Relevancy,
                    Reasoning = evaluationResult.Reasoning
                };
                return true;
            }
            catch
            {
                newsArticle = null;
                return false;
            }
        }
    }
}
