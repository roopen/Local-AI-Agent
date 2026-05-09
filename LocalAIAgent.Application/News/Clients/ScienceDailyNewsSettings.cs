using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class ScienceDailyNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "ScienceDailyClient";
        public override string DisplayName => "Science Daily";
        public override string BaseUrl => "https://www.sciencedaily.com/rss";
        public override string Language => "en";
        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/all.xml",
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
