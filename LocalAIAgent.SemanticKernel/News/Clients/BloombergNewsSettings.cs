using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class BloombergNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "BloombergClient";
        public override string BaseUrl => "https://feeds.bloomberg.com";

        public override List<string> GetNewsUrls()
        {
            return [
                $"{BaseUrl}/markets/news.rss",
                $"{BaseUrl}/technology/news.rss",
                $"{BaseUrl}/politics/news.rss",
                $"{BaseUrl}/wealth/news.rss",
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
