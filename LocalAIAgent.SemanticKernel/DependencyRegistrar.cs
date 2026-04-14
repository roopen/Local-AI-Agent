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
        public static IServiceCollection AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IGetNewsUseCase, GetNewsUseCase>();
            services.AddSingleton<INewsService, NewsService>();
            services.AddMemoryCache();
            services.AddSingleton<ChatContextStore>();
            services.AddSingleton<IClock>(SystemClock.Instance);
            AIOptions aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()!;
            if (aiOptions is not null)
            {
                if (string.IsNullOrWhiteSpace(aiOptions.ModelId))
                    aiOptions.ModelId = "unsloth/gemma-4-e4b-it";
            }

            services.AddKernel().GetSemanticKernelBuilder(aiOptions);
            services.AddSingleton(aiOptions);
            services.AddMemoryCache();

            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();
            services.AddScoped<IGetTranslationUseCase, GetTranslationUseCase>();
            services.AddScoped<INewsChatUseCase, NewsChatUseCase>();
            services.AddScoped<ILoadLLMUseCase, LoadLLMUseCase>();
            services.AddScoped<IGetLMStudioModelsUseCase, GetLMStudioModelsUseCase>();
            services.AddScoped<IDownloadLLMModelUseCase, DownloadLLMModelUseCase>();

            services.AddNewsClients();

            return services;
        }

        public static IKernelBuilder GetSemanticKernelBuilder(this IKernelBuilder kernelBuilder, AIOptions aiOptions)
        {
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();

            kernelBuilder.Services.AddSingleton(aiOptions);

            kernelBuilder.Plugins.AddFromType<TimeService>();

            kernelBuilder.AddVectorStoreTextSearch<NewsItem>();
            kernelBuilder.Services.AddInMemoryVectorStore();

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
