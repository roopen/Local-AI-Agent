using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class LeMondeNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "LeMondeClient";
        public override string DisplayName => "Le Monde";
        public override string BaseUrl => "https://www.lemonde.fr/en/rss";


        public override string Language => "en";
        public override List<string> GetNewsUrls()
        {
            return
                [
                    $"{BaseUrl}/une.xml",
                ];
        }

        public override void AddHttpClient(IServiceCollection services)
        {
            services.AddHttpClient(ClientName, client =>
            {
                client.BaseAddress = new Uri(BaseUrl);
            });
        }
    }
}
