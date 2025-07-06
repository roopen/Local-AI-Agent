using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class LibertyTimesTaiwanNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "LibertyTimesTaiwanClient";
        public override string BaseUrl => "https://news.ltn.com.tw/rss";

        public override bool RequiresTranslation => true;

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/all.xml",
                $"{BaseUrl}/politics.xml",
                $"{BaseUrl}/society.xml",
                $"{BaseUrl}/world.xml",
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
