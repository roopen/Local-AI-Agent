using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class LeMondeNewsSettings : INewsClientSettings
    {
        public string ClientName => "LeMondeClient";

        public string BaseUrl => "https://www.lemonde.fr/en/rss/";

        public string TopNews = "une.xml";

        public List<string> GetNewsUrls()
        {
            return [
                TopNews,
                ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
            });
        }
    }
}
