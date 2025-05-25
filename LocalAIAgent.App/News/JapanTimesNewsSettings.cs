using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal class JapanTimesNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "JapanTimes";
        public override string BaseUrl => "https://www.japantimes.co.jp";

        public override List<string> GetNewsUrls()
        {
            return
                [
                    $"{BaseUrl}/feed",
                ];
        }

        public override void AddHttpClient(IServiceCollection services)
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
