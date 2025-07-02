using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using LocalAIAgent.SemanticKernel.News;
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
            services.AddSingleton<IClock>(SystemClock.Instance);
            services.AddKernel().GetSemanticKernelBuilder();
            IConfiguration configuration = services.AddConfigurations();
            AIOptions aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()!;
            services.AddSingleton(aiOptions);

            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();

            services.AddNewsClients();

            return services;
        }

        public static IKernelBuilder GetSemanticKernelBuilder(this IKernelBuilder kernelBuilder)
        {
            kernelBuilder.Services.AddNewsClients();
            kernelBuilder.Services.AddSingleton<ChatService>();
            kernelBuilder.Services.AddSingleton<ChatContext>();
            //kernelBuilder.Services.AddSingleton<RAGService>();
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, EmbeddingService>();

            IConfiguration configuration = kernelBuilder.Services.AddConfigurations();
            AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()
                ?? throw new ArgumentNullException("AIOptions not found in configuration.");
            kernelBuilder.Services.AddSingleton(aiOptions);

            kernelBuilder.Plugins.AddFromType<TimeService>();
            //kernelBuilder.Plugins.AddFromType<RAGService>();

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
    }
}
