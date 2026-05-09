using System.ClientModel;
using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace LocalAIAgent.SemanticKernel
{
    public static class DependencyRegistrar
    {
        public const string GeneralChatClient = "General";
        public const string TranslationChatClient = "Translation";

        public static IServiceCollection AddSemanticKernel(this IServiceCollection services, IConfiguration configuration)
        {
            AIOptions aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>()
                ?? throw new InvalidOperationException("AIOptions section is missing from configuration.");
            if (string.IsNullOrWhiteSpace(aiOptions.ModelId))
                aiOptions.ModelId = "unsloth/gemma-4-e4b-it";

            services.AddSingleton(aiOptions);
            services.AddMemoryCache();

            services.AddKeyedSingleton<IChatClient>(GeneralChatClient, (sp, _) =>
                BuildChatClient(aiOptions, aiOptions.ModelId, TimeSpan.FromSeconds(90)));

            services.AddKeyedSingleton<IChatClient>(TranslationChatClient, (sp, _) =>
                BuildChatClient(aiOptions, aiOptions.LanguageModelId, timeout: null));

            services.AddScoped<IGetNewsUseCase, GetNewsUseCase>();
            services.AddSingleton<INewsService, NewsService>();
            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();
            services.AddScoped<IGetTranslationUseCase, GetTranslationUseCase>();
            services.AddScoped<INewsChatUseCase, NewsChatUseCase>();
            services.AddScoped<ILoadLLMUseCase, LoadLLMUseCase>();
            services.AddScoped<IGetLMStudioModelsUseCase, GetLMStudioModelsUseCase>();
            services.AddScoped<IDownloadLLMModelUseCase, DownloadLLMModelUseCase>();

            services.AddNewsClients();

            return services;
        }

        private static IChatClient BuildChatClient(AIOptions aiOptions, string modelId, TimeSpan? timeout)
        {
            OpenAIClientOptions clientOptions = new()
            {
                Endpoint = new Uri(aiOptions.EndpointUrl),
            };
            if (timeout.HasValue)
                clientOptions.NetworkTimeout = timeout.Value;

            string apiKey = string.IsNullOrEmpty(aiOptions.ApiKey) ? "no-key" : aiOptions.ApiKey;
            OpenAIClient openAIClient = new(new ApiKeyCredential(apiKey), clientOptions);
            return openAIClient.GetChatClient(modelId).AsIChatClient();
        }
    }
}
