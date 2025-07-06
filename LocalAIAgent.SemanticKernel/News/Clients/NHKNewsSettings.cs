using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class NHKNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NipponHōsōKyōkaiClient";
        public override string BaseUrl => "https://www3.nhk.or.jp/rss/news";

        public override bool RequiresTranslation => true;

        public override List<string> GetNewsUrls()
        {
            return
                [
                    $"{BaseUrl}/cat0.xml",
                    $"{BaseUrl}/cat1.xml",
                    $"{BaseUrl}/cat3.xml",
                    $"{BaseUrl}/cat4.xml",
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
