using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News.Clients
{
    internal class DeutscheWelleNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "DeutscheWelleClient";
        public override string BaseUrl => "https://rss.dw.com/atom/";

        public override List<string> GetNewsUrls()
        {
            return [
                $"{BaseUrl}/rss-en-top",
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
