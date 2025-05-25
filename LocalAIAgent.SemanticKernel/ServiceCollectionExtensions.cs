using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.News;
using LocalAIAgent.SemanticKernel.RAG.Embedding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.SemanticKernel.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        internal static void AddNewsClients(this IServiceCollection services)
        {
            List<BaseNewsClientSettings> newsClientSettings = typeof(BaseNewsClientSettings).Assembly
                .GetTypes()
                .Where(t => typeof(BaseNewsClientSettings).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(Activator.CreateInstance)
                .Cast<BaseNewsClientSettings>()
                .ToList();

            foreach (BaseNewsClientSettings clientSettings in newsClientSettings)
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
    }
}
