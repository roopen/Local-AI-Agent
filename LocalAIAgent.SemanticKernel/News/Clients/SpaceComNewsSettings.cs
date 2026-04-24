using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class SpaceComNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "SpaceComClient";
        public override string BaseUrl => "https://www.space.com";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/feeds.xml",
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
