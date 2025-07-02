using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace LocalAIAgent.SemanticKernel.News
{
    public interface IEvaluateNewsUseCase
    {
        Task<List<NewsItem>> EvaluateArticles(List<NewsItem> articles, UserPreferences userPreferences);
    }

    internal class EvaluateNewsUseCase(
        IChatCompletionService chatCompletion,
        Kernel kernel,
        AIOptions options) : IEvaluateNewsUseCase
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

            return result;
        }
    }
}
