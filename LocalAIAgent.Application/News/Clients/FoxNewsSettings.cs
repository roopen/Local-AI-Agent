using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class FoxNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "FoxNewsClient";
        public override string DisplayName => "Fox News";
        public override string BaseUrl => "https://moxie.foxnews.com/google-publisher";


        public override string Language => "en";
        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/us.xml",
                $"{BaseUrl}/world.xml",
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
