using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class DeutscheWelleNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "DeutscheWelleClient";
        public override string DisplayName => "Deutsche Welle";
        public override string BaseUrl => "https://rss.dw.com/atom/";


        public override string Language => "en";
        public override List<string> GetNewsUrls()
        {
            return [
                $"{BaseUrl}/rss-en-top",
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
