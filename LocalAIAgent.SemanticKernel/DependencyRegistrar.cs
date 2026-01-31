using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.News.AI;
using LocalAIAgent.SemanticKernel.RAG.Embedding;
using LocalAIAgent.SemanticKernel.Time;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using NodaTime;

namespace LocalAIAgent.SemanticKernel
{
    public static class DependencyRegistrar
    {
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
        {
            services.AddScoped<IGetNewsUseCase, GetNewsUseCase>();
            services.AddSingleton<INewsService, NewsService>();
            services.AddSingleton<ChatContext>();
            services.AddMemoryCache();
            services.AddSingleton<ChatContextStore>();
            services.AddSingleton<IClock>(SystemClock.Instance);
            services.AddKernel().GetSemanticKernelBuilder();
            IConfiguration configuration = services.AddConfigurations();
            AIOptions aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()!;
            services.AddSingleton(aiOptions);

            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();
            services.AddScoped<IGetTranslationUseCase, GetTranslationUseCase>();
            services.AddScoped<INewsChatUseCase, NewsChatUseCase>();
            services.AddScoped<ILoadLLMUseCase, LoadLLMUseCase>();

            services.AddNewsClients();

            return services;
        }

        public static IKernelBuilder GetSemanticKernelBuilder(this IKernelBuilder kernelBuilder)
        {
            kernelBuilder.Services.AddSingleton<ChatService>();
            kernelBuilder.Services.AddSingleton<ChatContext>();
            //kernelBuilder.Services.AddSingleton<RAGService>();
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();

            IConfiguration configuration = kernelBuilder.Services.AddConfigurations();
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
            AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()
                ?? throw new ArgumentNullException("AIOptions not found in configuration.");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            kernelBuilder.Services.AddSingleton(aiOptions);

            kernelBuilder.Plugins.AddFromType<TimeService>();
            //kernelBuilder.Plugins.AddFromType<RAGService>();

            kernelBuilder.AddVectorStoreTextSearch<NewsItem>();
            kernelBuilder.Services.AddInMemoryVectorStore();

            //kernelBuilder.AddGoogleAIGeminiChatCompletion(aiOptions.ModelId, aiOptions.ApiKey);

            kernelBuilder
                .AddOpenAIChatCompletion(
                    modelId: aiOptions.ModelId,
                    apiKey: aiOptions.ApiKey,
                    endpoint: new Uri(aiOptions.EndpointUrl),
                    serviceId: "General",
                    httpClient: new HttpClient() { Timeout = TimeSpan.FromSeconds(90) }
                );

            kernelBuilder
                .AddOpenAIChatCompletion(
                    modelId: aiOptions.LanguageModelId,
                    apiKey: aiOptions.ApiKey,
                    endpoint: new Uri(aiOptions.EndpointUrl),
                    serviceId: "Translation"
                );

            return kernelBuilder;
        }
    }
}
