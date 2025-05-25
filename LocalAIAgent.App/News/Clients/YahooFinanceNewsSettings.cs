using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News.Clients
{
    internal class YahooFinanceNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "YahooFinanceClient";
        public override string BaseUrl => "https://finance.yahoo.com/news";

        public override List<string> GetNewsUrls()
        {
            return [
                $"{BaseUrl}/rssindex",
            ];
        }

        public override void AddHttpClient(IServiceCollection services)
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
