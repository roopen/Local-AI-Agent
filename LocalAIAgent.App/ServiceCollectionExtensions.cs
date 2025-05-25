using LocalAIAgent.App.Chat;
using LocalAIAgent.App.News;
using LocalAIAgent.App.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;

namespace LocalAIAgent.App.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        internal static void AddNewsClients(this IServiceCollection services)
        {
            List<INewsClientSettings> newsClientSettings = typeof(INewsClientSettings).Assembly
                .GetTypes()
                .Where(t => typeof(INewsClientSettings).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(Activator.CreateInstance)
                .Cast<INewsClientSettings>()
                .ToList();

            foreach (INewsClientSettings clientSettings in newsClientSettings)
            {
                services.AddSingleton(clientSettings);
                clientSettings.AddHttpClient(services);
            }
        }

        /// <summary>
        /// Adds the configuration settings from appsettings.json to the service collection.
        /// </summary>
        /// <returns>The IConfiguration if it needs to be used before buildign the kernel.</returns>
        internal static IConfiguration AddConfigurations(this IServiceCollection services)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            services.Configure<AIOptions>(configuration.GetSection("AIOptions"));
            services.Configure<EmbeddingOptions>(configuration.GetSection("EmbeddingOptions"));

            return configuration;
        }

        internal static async Task InitializeVectorDatabase(this Kernel kernel)
        {
            NewsService? newsService = kernel.Services.GetService<NewsService>();

            if (newsService is not null) await newsService.LoadAllNews();

            else Console.WriteLine("Warning: NewsService is not registered in the kernel. Vector database initialization skipped.");
        }

        internal static async Task StartAIChat(this Kernel kernel, AIOptions options)
        {
            ChatService chatService = new(
                kernel.Services.GetService<IChatCompletionService>()!,
                kernel,
                options,
                kernel.Services.GetService<ChatContext>()!
            );

            await kernel.LoadUserPrompt(chatService);
            await kernel.InitializeVectorDatabase();

            await chatService.StartChat();
        }

        private static async Task LoadUserPrompt(this Kernel kernel, ChatService chatService)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string userPreferencesPrompt = GetUserPreferencesPrompt();

            List<string> bannedWords = await chatService.GetUnwantedTopics(userPreferencesPrompt);
            ChatContext chatContext = kernel.Services.GetService<ChatContext>()!;
            chatContext.UserDislikes = bannedWords;
            chatContext.UserPrompt = userPreferencesPrompt;

            stopwatch.Stop();
            Console.WriteLine($"UserPrompt.txt loaded into ChatContext in {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Attempts to read a user preferences prompt from ./UserPrompt.txt.
        /// </summary>
        static string GetUserPreferencesPrompt()
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
