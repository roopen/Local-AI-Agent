using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class NRKNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NRKNewsClient";
        public override string BaseUrl => "https://www.nrk.no";

        public override bool RequiresTranslation => true;

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
