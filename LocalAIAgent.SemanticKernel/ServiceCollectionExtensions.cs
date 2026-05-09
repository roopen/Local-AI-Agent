using LocalAIAgent.SemanticKernel.News;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.Extensions
{
    public static class ServiceCollectionExtensions
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
    }
}
