using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;

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

            await chatService.StartConsoleChat();
        }

        public static async Task<UserPreferences> ReadUserPreferencesFromFile(ChatService chatService)
        {
            return new UserPreferences
            {
                Prompt = GetUserPreferencesPrompt(),
                Interests = await chatService.GetInterestingTopicsList(GetUserPreferencesPrompt()),
                Dislikes = await chatService.GetDislikedTopicsList(GetUserPreferencesPrompt())
            };
        }

        public static async Task LoadUserPromptIntoChatContext(this Kernel kernel, ChatService chatService, string userPreferencesPrompt)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            UserPreferences userPreferences = await ReadUserPreferencesFromFile(chatService);
            ChatContext chatContext = kernel.Services.GetRequiredService<ChatContext>();
            chatContext.UserDislikes = userPreferences.Dislikes;
            chatContext.UserInterests = userPreferences.Interests;
            chatContext.UserPrompt = userPreferencesPrompt;

            stopwatch.Stop();
            Console.WriteLine($"User preferences prompt loaded into ChatContext in {stopwatch.ElapsedMilliseconds} ms.");
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
