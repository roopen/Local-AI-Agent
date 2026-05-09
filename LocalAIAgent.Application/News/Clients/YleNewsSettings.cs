using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class YleNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "YleClient";
        public override string BaseUrl => "https://yle.fi/rss";
        public override string Language => "fi";
        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/uutiset/paauutiset",
                $"{BaseUrl}/t/18-819/fi",
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
