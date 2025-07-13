using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class ElMundoNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "ElMundoClient";
        public override string BaseUrl => "https://e00-elmundo.uecdn.es/elmundo/rss";

        public override bool RequiresTranslation => true;

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/portada.xml",
                $"{BaseUrl}/espana.xml",
                $"{BaseUrl}/internacional.xml",
                $"{BaseUrl}/union_europea.xml",
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
