using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class YahooFinanceNewsSettings : INewsClientSettings
    {
        public string ClientName => "YahooFinanceClient";
        public string BaseUrl => "https://finance.yahoo.com/news/";
        public string UserAgent = "Mozilla/5.0";
        public string Host = "finance.yahoo.com";

        public string RssIndexUrl = "rssindex";

        public List<string> GetNewsUrls()
        {
            return [
                RssIndexUrl
            ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Host = Host;
            });
        }
    }
}
