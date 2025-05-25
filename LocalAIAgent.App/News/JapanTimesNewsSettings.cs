using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class JapanTimesNewsSettings : INewsClientSettings
    {
        public string ClientName => "JapanTimes";
        public string UserAgent = "Mozilla/5.0";
        public string Host = "japantimes.co.jp";
        public string BaseUrl => "https://www.japantimes.co.jp";

        public List<string> GetNewsUrls()
        {
            return
                [
                    $"{BaseUrl}/feed",
                ];
        }

        public void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true
            });
        }
    }
}
