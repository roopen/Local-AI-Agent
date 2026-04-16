using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class PravdaNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "PravdaClient";
        public override string BaseUrl => "https://www.pravda.com.ua/eng/rss/view_news/";

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
