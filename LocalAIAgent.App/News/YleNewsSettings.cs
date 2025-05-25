using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class YleNewsSettings : INewsClientSettings
    {
        public string ClientName => "YleNewsClient";
        public string BaseUrl => "https://yle.fi/rss";
        public string UserAgent = "Mozilla/5.0";

        public List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/uutiset/paauutiset",
            ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            });
        }
    }
}
