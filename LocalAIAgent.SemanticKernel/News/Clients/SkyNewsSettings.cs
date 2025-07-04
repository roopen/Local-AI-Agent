using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class SkyNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "SkyNewsClient";
        public override string BaseUrl => "https://feeds.skynews.com/feeds/rss";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/home.xml",
            ];
        }

        public override void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.Host = Host;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true
            });
        }
    }
}
