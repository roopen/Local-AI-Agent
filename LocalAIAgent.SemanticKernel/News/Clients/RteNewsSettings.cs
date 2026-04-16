using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class RteNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "RteClient";
        public override string BaseUrl => "https://www.rte.ie/feeds/rss/";

        public override List<string> GetNewsUrls()
        {
            return new List<string> { $"{BaseUrl}?index=/news/&limit=100" };
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
