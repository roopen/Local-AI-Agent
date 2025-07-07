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
                $"User is reading a news summary. " +
                $"Your job is to talk about the news summary provided in this prompt with the user. Explain the terms mentioned in the article " +
                $"history behind groups and peoples and help the user understand the broader picture.\n" +
                $"Get more information as necessary with tools at your disposal.\n" +
                $"The news summary the user is reading: {userMessage}";

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
