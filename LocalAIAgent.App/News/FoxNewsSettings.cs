using Microsoft.Extensions.DependencyInjection;

namespace Local_AI_Agent.News
{
    internal class FoxNewsSettings : INewsClientSettings
    {
        public static string ClientName = "FoxNewsClient";
        public static string UserAgent = "Mozilla/5.0";
        public static string BaseUrl = "https://moxie.foxnews.com/google-publisher/";
        public static string Host = "moxie.foxnews.com";

        public static string UsaNewsUrl = "us.xml";
        public static string WorldNewsUrl = "world.xml";

        public static List<string> GetNewsUrls()
        {
            return
            [
                UsaNewsUrl,
                WorldNewsUrl
            ];
        }
    }

    internal static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFoxNewsClient(this IServiceCollection services)
        {
            services.AddHttpClient(FoxNewsSettings.ClientName, client =>
            {
                client.BaseAddress = new Uri(FoxNewsSettings.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(FoxNewsSettings.UserAgent);
                client.DefaultRequestHeaders.Host = FoxNewsSettings.Host;
            });
            return services;
        }
    }
}
