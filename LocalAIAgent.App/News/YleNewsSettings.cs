using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class YleNewsSettings : INewsClientSettings
    {
        public string ClientName => "YleNewsClient";
        public string BaseUrl => "https://yle.fi/";
        public string UserAgent = "Mozilla/5.0";

        public string MainHeadlinesUrl = "/rss/uutiset/paauutiset";
        // These contain duplicate news. Needs to be handled separately.
        //public static string FinanceNewsUrl = "rss/t/18-19274/fi";
        //public static string WorldNewsUrl = "/rss/t/18-34953/fi";
        //public static string FinlandNewsUrl = "/rss/t/18-34837/fi";

        public List<string> GetNewsUrls()
        {
            return
            [
                MainHeadlinesUrl,
                //FinanceNewsUrl,
                //WorldNewsUrl,
                //FinlandNewsUrl
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
