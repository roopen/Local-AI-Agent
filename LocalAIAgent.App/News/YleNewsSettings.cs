using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class YleNewsSettings : INewsClientSettings
    {
        public string ClientName { get => "YleNewsClient"; }
        public static string UserAgent = "Mozilla/5.0";
        public static string BaseUrl = "https://yle.fi/";

        public static string MainHeadlinesUrl = "/rss/uutiset/paauutiset";
        public static string FinanceNewsUrl = "rss/t/18-19274/fi";
        public static string WorldNewsUrl = "/rss/t/18-34953/fi";
        public static string FinlandNewsUrl = "/rss/t/18-34837/fi";

        public List<string> GetNewsUrls()
        {
            return
            [
                MainHeadlinesUrl,
                FinanceNewsUrl,
                WorldNewsUrl,
                FinlandNewsUrl
            ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            });
        }
    }
}
