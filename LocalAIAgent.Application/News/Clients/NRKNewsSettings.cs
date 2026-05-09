using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class NRKNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NorwegianBroadcastingCorporationClient";
        public override string BaseUrl => "https://www.nrk.no";
        public override string Language => "no";
        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/toppsaker.rss",
                $"{BaseUrl}/viten/toppsaker.rss",
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
