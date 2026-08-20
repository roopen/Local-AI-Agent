using System.Text;
using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.AI;

namespace LocalAIAgent.Application.News.AI
{
    public interface INewsChatUseCase
    {
        Task<ExpandedNewsResult> GetExpandedNewsAsync(string article, int? userPreferencesId = null);
    }

    internal class NewsChatUseCase(
        ILlmRuntimeManager runtimeManager) : INewsChatUseCase
    {
        public async Task<ExpandedNewsResult> GetExpandedNewsAsync(string article, int? userPreferencesId = null)
        {
            LlmRuntimeSnapshot runtime = runtimeManager.GetRequiredSnapshot(userPreferencesId);
            IChatClient chatClient = runtime.ChatClient;
            AIOptions options = runtime.Options;
            string prompt =
                "User is reading a news summary. " +
                "Translate the news to English. If the article is already in English, don't include a translation." +
                "Explain any abbreviations, people, groups, entities mentioned in the news.\n" +
                "Keep your answers short and concise." +
                "Respond using the following json schema: " +
                "{\r\n  \"articleWasTranslated\": true,\r\n  \"translation\": \"string\",\r\n  \"termsAndExplanations\": [\r\n    {\r\n      \"key\": {\r\n        \"term\": \"string\"\r\n      },\r\n      \"value\": {\r\n        \"explanation\": \"string\"\r\n      }\r\n    }\r\n  ]\r\n}";

            ChatOptions chatOptions = options.BuildChatOptions(ChatResponseFormat.Json);

            List<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, prompt),
                new ChatMessage(ChatRole.User, article),
            ];

            StringBuilder responseBuilder = new();
            await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(messages, chatOptions).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    responseBuilder.Append(update.Text);
            }

            return ExpandedNewsResult.FromJson(responseBuilder.ToString());
        }
    }
}
