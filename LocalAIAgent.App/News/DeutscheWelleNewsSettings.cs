using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class DeutscheWelleNewsSettings : INewsClientSettings
    {
        public string ClientName => "DeutscheWelleClient";
        public string BaseUrl => "https://rss.dw.com/atom/";
        public string UserAgent = "Mozilla/5.0";
        public string Host = "rss.dw.com";

        public string TopNewsUrl = "rss-en-top";

        public List<string> GetNewsUrls()
        {
            return [
                TopNewsUrl
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
