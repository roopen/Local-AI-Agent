using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.Extensions;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.News.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LocalAIAgent.Application
{
    public static class DependencyRegistrar
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<ILlmRuntimeManager, LlmRuntimeManager>();

            services.AddScoped<IGetNewsUseCase, GetNewsUseCase>();
            services.AddSingleton<INewsService, NewsService>();
            services.AddSingleton<IFeedCatalog, FeedCatalog>();
            services.AddScoped<ICustomFeedFetcher, CustomFeedFetcher>();
            services.AddScoped<IFeedValidator, FeedValidator>();
            services.AddHttpClient("CustomFeedClient", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(PublicNetworkHttpHandler.Create);
            services.AddHttpClient("FeedValidatorClient", client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                // Shorter timeout — the user is waiting on this synchronously in the Add-Feed flow.
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .ConfigurePrimaryHttpMessageHandler(PublicNetworkHttpHandler.Create);
            services.AddScoped<IEvaluateNewsUseCase, EvaluateNewsUseCase>();
            services.AddScoped<IGetTranslationUseCase, GetTranslationUseCase>();
            services.AddScoped<INewsChatUseCase, NewsChatUseCase>();
            services.AddScoped<ILoadLLMUseCase, LoadLLMUseCase>();
            services.AddScoped<IGetLMStudioModelsUseCase, GetLMStudioModelsUseCase>();
            services.AddScoped<IDownloadLLMModelUseCase, DownloadLLMModelUseCase>();

            services.AddNewsClients();

            return services;
        }
    }
}
