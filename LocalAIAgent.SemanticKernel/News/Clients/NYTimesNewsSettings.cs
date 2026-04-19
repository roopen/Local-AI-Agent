using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class NYTimesNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NYTimesClient";
        public override string BaseUrl => "https://rss.nytimes.com";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/services/xml/rss/nyt/Americas.xml",
                $"{BaseUrl}/services/xml/rss/nyt/World.xml",
            ];
        }

        public override void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            });
        }
    }
}
