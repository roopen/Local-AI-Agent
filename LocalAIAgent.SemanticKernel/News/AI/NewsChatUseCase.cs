using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface INewsChatUseCase
    {
        IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage);
    }

    internal class NewsChatUseCase(
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
        Kernel kernel,
        AIOptions options) : INewsChatUseCase
    {
        private ChatHistory ChatHistory { get; } = [];

        public async IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage)
        {
            string prompt = $"You are a chat assistant." +
                $"User is reading the following news summary: {userMessage} " +
                $"Translate the news to English if it's not already in English. Explain any abbreviations, people, groups, entities mentioned in the news.\n" +
                $"Answer any questions the user has about the news article. " +
                $"Keep your answers short and concise. Format all entity breakdowns as bullet-point lists with bolded names.";

            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt, allowFunctionUse: true);

            ChatHistory.Clear();
            ChatHistory.AddUserMessage(userMessage);

            await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                                ChatHistory,
                                openAiSettings,
                                kernel)
                                .ConfigureAwait(false))
            {
                if (content is null)
                    continue;

                yield return content;
            }
        }
    }
}
