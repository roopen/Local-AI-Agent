using LocalAIAgent.App.News;
using LocalAIAgent.App.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
