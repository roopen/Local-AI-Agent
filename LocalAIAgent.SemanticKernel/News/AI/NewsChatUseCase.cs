using System.Text;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface INewsChatUseCase
    {
        Task<ExpandedNewsResult> GetExpandedNewsAsync(string article);
    }

    internal class NewsChatUseCase(
        [FromKeyedServices(DependencyRegistrar.GeneralChatClient)] IChatClient chatClient,
        AIOptions options) : INewsChatUseCase
    {
        public async Task<ExpandedNewsResult> GetExpandedNewsAsync(string article)
        {
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
