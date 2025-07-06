using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class YleNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "YleClient";
        public override string BaseUrl => "https://yle.fi/rss";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/news",
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
