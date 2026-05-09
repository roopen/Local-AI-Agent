using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class PostimeesNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "PostimeesClient";
        public override string DisplayName => "Postimees";
        public override string BaseUrl => "https://www.postimees.ee";
        public override string Language => "et";
        public override List<string> AdditionalHosts => ["pmo.ee"];

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/rss",
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
