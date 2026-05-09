using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class InfoqNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "InfoqClient";
        public override string BaseUrl => "https://feed.infoq.com/";

        public override List<string> GetNewsUrls()
        {
            return [BaseUrl];
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
