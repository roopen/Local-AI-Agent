using LocalAIAgent.App.News;
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
    }
}
