using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.RAG;
using LocalAIAgent.SemanticKernel.RAG.Embedding;
using LocalAIAgent.SemanticKernel.Time;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NodaTime;
using System.Diagnostics;

namespace LocalAIAgent.SemanticKernel
{
    public static class DependencyRegistrar
    {
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
        {
            Kernel kernel = GetSemanticKernel();

            services.AddSingleton(sp => GetSemanticKernel());

            return services;
        }

        private static Kernel GetSemanticKernel()
        {
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.Services.AddNewsClients();
            kernelBuilder.Services.AddSingleton<ChatService>();
            kernelBuilder.Services.AddSingleton<ChatContext>();
            kernelBuilder.Services.AddSingleton<RAGService>();
            kernelBuilder.Services.AddSingleton<NewsService>();
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();
            kernelBuilder.Services.AddSingleton<IClock>(SystemClock.Instance);

            IConfiguration configuration = kernelBuilder.Services.AddConfigurations();
            AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>() ?? throw new Exception("AIOptions not found in configuration.");
            kernelBuilder.Services.AddSingleton(aiOptions);

            kernelBuilder.Plugins.AddFromType<TimeService>();
            kernelBuilder.Plugins.AddFromType<NewsService>();

            kernelBuilder.AddVectorStoreTextSearch<NewsItem>();
            kernelBuilder.AddInMemoryVectorStore();
            //kernelBuilder.Services.AddInMemoryVectorStoreRecordCollection<int, NewsItem>("news");

#pragma warning disable SKEXP0070
            // Experimental Google Gemini support
            //kernelBuilder.AddGoogleAIGeminiChatCompletion(aiOptions.ModelId, aiOptions.ApiKey);
#pragma warning restore SKEXP0070

            kernelBuilder
                .AddOpenAIChatCompletion(
                    modelId: aiOptions.ModelId,
                    apiKey: aiOptions.ApiKey,
                    endpoint: new Uri(aiOptions.EndpointUrl)
                );

            Kernel kernel = kernelBuilder.Build();

            return kernel;
        }

        public static async Task StartAIChatInConsole(this Kernel kernel)
        {
            ChatService chatService = new(
                kernel.Services.GetService<IChatCompletionService>()!,
                kernel,
                kernel.Services.GetService<AIOptions>()!,
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
