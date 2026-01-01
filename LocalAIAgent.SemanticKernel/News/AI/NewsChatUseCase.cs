using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface INewsChatUseCase
    {
        IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage);

        Task<ExpandedNewsResult> GetExpandedNewsAsync(string article);
    }

    internal class NewsChatUseCase(
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
        Kernel kernel,
        AIOptions options) : INewsChatUseCase
    {
        private ChatHistory ChatHistory { get; } = [];

        public async Task<ExpandedNewsResult> GetExpandedNewsAsync(string article)
        {
            string prompt =
                $"User is reading a news summary. " +
                $"Translate the news to English. If the article is already in English, don't include a translation." +
                $"Explain any abbreviations, people, groups, entities mentioned in the news.\n" +
                $"Keep your answers short and concise." +
                $"Respond using the following json schema: " +
                $"{{\r\n  \"articleWasTranslated\": true,\r\n  \"translation\": \"string\",\r\n  \"termsAndExplanations\": [\r\n    {{\r\n      \"key\": {{\r\n        \"term\": \"string\"\r\n      }},\r\n      \"value\": {{\r\n        \"explanation\": \"string\"\r\n      }}\r\n    }}\r\n  ]\r\n}}";

            ChatHistory chatMessageContents = [];
            chatMessageContents.AddUserMessage(article);

            OpenAIPromptExecutionSettings settings = new()
            {
                ChatSystemPrompt = prompt,
                ResponseFormat = new ExpandedNewsResult(),
                FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                ReasoningEffort = ChatReasoningEffortLevel.High,
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            };

            var result = await chatCompletion.GetChatMessageContentAsync(
                chatMessageContents,
                settings,
                kernel);

            return ExpandedNewsResult.FromJson(result.Content);
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage)
        {
            string prompt = $"You are a chat assistant." +
                $"User is reading the following news summary: {userMessage} " +
                $"Answer any questions the user has about the news article. " +
                $"Keep your answers short and concise.";

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
