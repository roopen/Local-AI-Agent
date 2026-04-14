using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface INewsChatUseCase
    {
        IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(List<string> chatHistory);

        Task<ExpandedNewsResult> GetExpandedNewsAsync(string article);
    }

    internal class NewsChatUseCase(
        Kernel kernel,
        AIOptions options) : INewsChatUseCase
    {
        public async Task<ExpandedNewsResult> GetExpandedNewsAsync(string article)
        {
            string prompt =
                $"User is reading a news summary. " +
                $"Translate the news to English. If the article is already in English, don't include a translation." +
                $"Explain any abbreviations, people, groups, entities mentioned in the news.\n" +
                $"Keep your answers short and concise." +
                $"Respond using the following json schema: " +
                $"{{\r\n  \"articleWasTranslated\": true,\r\n  \"translation\": \"string\",\r\n  \"termsAndExplanations\": [\r\n    {{\r\n      \"key\": {{\r\n        \"term\": \"string\"\r\n      }},\r\n      \"value\": {{\r\n        \"explanation\": \"string\"\r\n      }}\r\n    }}\r\n  ]\r\n}}";

            ChatCompletionAgent agent = new()
            {
                Instructions = prompt,
                Kernel = kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    ServiceId = "General",
                    ModelId = options.ModelId,
                    ResponseFormat = new ExpandedNewsResult(),
                    FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    ReasoningEffort = ChatReasoningEffortLevel.High,
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                }),
            };

            ChatHistoryAgentThread thread = new();
            ChatMessageContent userMessage = new(AuthorRole.User, article);

            ChatMessageContent? response = null;
            await foreach (ChatMessageContent msg in agent.InvokeAsync(userMessage, thread).ConfigureAwait(false))
                response = msg;

            return ExpandedNewsResult.FromJson(response?.Content);
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(List<string> messages)
        {
            if (messages.Count == 0)
                yield break;

            string newsArticle = messages[0];

            string prompt = $"You are a chat assistant." +
                $"Answer any questions the user has about the news article provided. News article: {newsArticle}" +
                $"Keep your answers short and concise. Use tools only when required to.";

            ChatCompletionAgent agent = new()
            {
                Instructions = prompt,
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: true)),
            };

            ChatHistoryAgentThread thread = new();
            foreach (string msg in messages.SkipLast(1))
                thread.ChatHistory.AddUserMessage(msg);

            ChatMessageContent latestMessage = new(AuthorRole.User, messages[^1]);

            await foreach (StreamingChatMessageContent? content in agent.InvokeStreamingAsync(latestMessage, thread)
                                .ConfigureAwait(false))
            {
                if (content is null)
                    continue;

                yield return content;
            }
        }
    }
}
