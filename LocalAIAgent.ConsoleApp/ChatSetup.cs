using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalAIAgent.ConsoleApp
{
    internal static class ChatSetup
    {
        public static async Task StartAIChatInConsole(this Kernel kernel)
        {
            ChatService chatService = new(
                kernel.Services.GetService<IChatCompletionService>()!,
                kernel,
                kernel.Services.GetService<AIOptions>()!,
                kernel.Services.GetService<ChatContext>()!
            );

            await kernel.LoadUserPromptIntoChatContext(chatService, GetUserPreferencesPrompt());
            await kernel.InitializeVectorDatabase();

            await chatService.StartChat();
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
