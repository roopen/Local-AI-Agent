using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Text;

namespace LocalAIAgent.App.Chat
{
    internal class ChatService(IChatCompletionService chatCompletion, Kernel kernel, AIOptions options)
    {
        private const string ChatSystemPrompt = "You are an AI assistant. " +
            "When asked about news, you curate the current news according to user's preferences " +
            "Try to find the most significant news. " +
            "Use the following template for news reporting and fill information from the json response: \n" +
            "**Category:** Title - Summary (Source) [Link] \n The news information will be in json format. " +
            "Keep your answers short. Be willing to discuss any news with the user.";

        public async Task StartChat()
        {
            ChatHistory chatHistory = [];

            StringBuilder fullAssistantContent = new();

            OpenAIPromptExecutionSettings openAiSettings = GetOpenAIPromptExecutionSettings(options);

            while (true)
            {
                Console.Write("User: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) { break; }
                chatHistory.AddUserMessage(input);
                Console.WriteLine("Assistant: ");

                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    openAiSettings,
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

        private static OpenAIPromptExecutionSettings GetOpenAIPromptExecutionSettings(AIOptions options)
        {
            return new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = ChatSystemPrompt + GetUserPreferencesPrompt(),
                ReasoningEffort = ChatReasoningEffortLevel.High,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = (double)options.Temperature,
                TopP = (double)options.TopP,
                FrequencyPenalty = (double)options.FrequencyPenalty,
                PresencePenalty = (double)options.PresencePenalty,
            };
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
