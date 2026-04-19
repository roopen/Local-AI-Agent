using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class IlFattoQuotidianoNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "IlFattoQuotidianoClient";
        public override string BaseUrl => "https://www.ilfattoquotidiano.it";

        public override bool RequiresTranslation => true;

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/feed/",
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