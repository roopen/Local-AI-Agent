using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class DotNetKicksNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "DotNetKicksClient";
        public override string BaseUrl => "https://dotnetkicks.com/feeds/rss";

        public override List<string> GetNewsUrls()
        {
            return new List<string> { BaseUrl };
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
