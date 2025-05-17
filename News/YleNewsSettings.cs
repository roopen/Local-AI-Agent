using Microsoft.Extensions.DependencyInjection;

namespace Local_AI_Agent.News
{
    internal class YleNewsSettings : INewsClientSettings
    {
        public static string ClientName = "YleNewsClient";
        public static string UserAgent = "Local-AI-Agent";
        public static string BaseUrl = "https://yle.fi/";

        public static string MainHeadlinesUrl = "/rss/uutiset/paauutiset";
        public static string FinanceNewsUrl = "rss/t/18-19274/fi";
        public static string WorldNewsUrl = "/rss/t/18-34953/fi";
        public static string FinlandNewsUrl = "/rss/t/18-34837/fi";

        public static List<string> GetNewsUrls()
        {
            return
            [
                MainHeadlinesUrl,
                FinanceNewsUrl,
                WorldNewsUrl,
                FinlandNewsUrl
            ];
        }
    }

    internal static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddYleNewsClient(this IServiceCollection services)
        {
            services.AddHttpClient(YleNewsSettings.ClientName, client =>
            {
                client.BaseAddress = new Uri(YleNewsSettings.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(YleNewsSettings.UserAgent);
            });

            return services;
        }
    }
}
