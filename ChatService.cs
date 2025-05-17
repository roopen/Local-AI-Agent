using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Text;

namespace Local_AI_Agent
{
    internal class ChatService(IChatCompletionService chatCompletion, Kernel kernel)
    {
        private const string ChatSystemPrompt = "You are an AI assistant with access to news services. Your job is to find news that interest the user. " +
            "When asked about news, default to listing the top news with each news article on a separate line. Mention the media behind the article. " +
            "Use what you know about what the user likes to find news articles and hide what the user dislikes. Keep your answers short and do not " +
            "mention what you are filtering for unless asked.";

        public async Task StartChat()
        {
            ChatHistory chatHistory = [];

            StringBuilder fullAssistantContent = new();

            while (true)
            {
                Console.Write("User: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) { break; }
                chatHistory.AddUserMessage(input);
                Console.WriteLine("Assistant: ");

                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    new OpenAIPromptExecutionSettings
                    {
                        ChatSystemPrompt = ChatSystemPrompt + GetUserPreferencesPrompt(),
                        ReasoningEffort = ChatReasoningEffortLevel.High,
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    },
                    kernel)
                    .ConfigureAwait(false))
                {
                    Console.Write(content.Content);
                    fullAssistantContent.Append(content.Content);
                }

                chatHistory.AddAssistantMessage(fullAssistantContent.ToString());
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Attempts to read a user preferences prompt from ./UserPrompt.txt.
        /// </summary>
        private static string GetUserPreferencesPrompt()
        {
            if (!File.Exists("UserPrompt.txt"))
            {
                Console.WriteLine("UserPrompt.txt not found.");
                return string.Empty;
            }
            else
            {
                Console.WriteLine("UserPrompt.txt found.");
                return File.ReadAllText("UserPrompt.txt");
            }
        }
    }
}
