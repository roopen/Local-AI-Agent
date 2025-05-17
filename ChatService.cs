using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;

namespace Local_AI_Agent
{
    internal class ChatService(IChatCompletionService chatCompletion, Kernel kernel)
    {
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
                    new OpenAIPromptExecutionSettings { ReasoningEffort = "high", FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
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
    }
}
