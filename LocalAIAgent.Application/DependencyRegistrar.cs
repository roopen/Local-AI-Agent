using System.ClientModel;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.Extensions;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenAI;

namespace LocalAIAgent.Application
{
    public static class DependencyRegistrar
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            AIOptions aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()
                ?? throw new InvalidOperationException("AIOptions section is missing from configuration.");
            if (string.IsNullOrWhiteSpace(aiOptions.ModelId))
                aiOptions.ModelId = "unsloth/gemma-4-e4b-it";

            services.AddSingleton(aiOptions);
            services.AddMemoryCache();
            services.TryAddSingleton(TimeProvider.System);

            services.AddSingleton<IChatClient>(_ => BuildChatClient(aiOptions));

            services.AddScoped<IGetNewsUseCase, GetNewsUseCase>();
            services.AddSingleton<INewsService, NewsService>();
            services.AddSingleton<IFeedCatalog, FeedCatalog>();
            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();
            services.AddScoped<IGetTranslationUseCase, GetTranslationUseCase>();
            services.AddScoped<INewsChatUseCase, NewsChatUseCase>();
            services.AddScoped<ILoadLLMUseCase, LoadLLMUseCase>();
            services.AddScoped<IGetLMStudioModelsUseCase, GetLMStudioModelsUseCase>();
            services.AddScoped<IDownloadLLMModelUseCase, DownloadLLMModelUseCase>();

            services.AddNewsClients();

            return services;
        }

        private static IChatClient BuildChatClient(AIOptions aiOptions)
        {
            OpenAIClientOptions clientOptions = new()
            {
                Endpoint = new Uri(aiOptions.EndpointUrl),
                NetworkTimeout = TimeSpan.FromSeconds(90),
            };

            string apiKey = string.IsNullOrEmpty(aiOptions.ApiKey) ? "no-key" : aiOptions.ApiKey;
            OpenAIClient openAIClient = new(new ApiKeyCredential(apiKey), clientOptions);
            return openAIClient.GetChatClient(aiOptions.ModelId).AsIChatClient();
        }
    }
}
