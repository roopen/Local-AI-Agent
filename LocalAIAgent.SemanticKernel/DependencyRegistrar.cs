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
using NodaTime;
using System.Diagnostics;

namespace LocalAIAgent.SemanticKernel
{
    public static class DependencyRegistrar
    {
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
        {
            IKernelBuilder kernelBuilder = GetSemanticKernelBuilder();

            services.AddSingleton(sp => kernelBuilder.Build());

            return services;
        }

        public static IKernelBuilder GetSemanticKernelBuilder()
        {
            IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.Services.AddNewsClients();
            kernelBuilder.Services.AddSingleton<ChatService>();
            kernelBuilder.Services.AddSingleton<ChatContext>();
            kernelBuilder.Services.AddSingleton<RAGService>();
            kernelBuilder.Services.AddSingleton<INewsService, NewsService>();
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();
            kernelBuilder.Services.AddSingleton<IClock>(SystemClock.Instance);

            IConfiguration configuration = kernelBuilder.Services.AddConfigurations();
            AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>() ?? throw new Exception("AIOptions not found in configuration.");
            kernelBuilder.Services.AddSingleton(aiOptions);

            kernelBuilder.Plugins.AddFromType<TimeService>();
            kernelBuilder.Plugins.AddFromType<RAGService>();

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

            return kernelBuilder;
        }

        public static async Task LoadUserPromptIntoChatContext(this Kernel kernel, ChatService chatService, string userPreferencesPrompt)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<string> dislikedTopics = await chatService.GetDislikedTopicsList(userPreferencesPrompt);
            List<string> wantedTopics = await chatService.GetInterestingTopicsList(userPreferencesPrompt);
            ChatContext chatContext = kernel.Services.GetRequiredService<ChatContext>();
            chatContext.UserDislikes = dislikedTopics;
            chatContext.UserInterests = wantedTopics;
            chatContext.UserPrompt = userPreferencesPrompt;

            stopwatch.Stop();
            Console.WriteLine($"User preferences prompt loaded into ChatContext in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }
}
