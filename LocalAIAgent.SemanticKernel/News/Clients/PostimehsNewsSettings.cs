using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class PostimeesNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "PostimeesClient";
        public override string BaseUrl => "https://www.postimees.ee";

        public override bool RequiresTranslation => true;
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
