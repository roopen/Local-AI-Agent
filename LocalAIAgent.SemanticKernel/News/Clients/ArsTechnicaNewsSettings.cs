using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class ArsTechnicaNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "ArsTechnicaClient";
        public override string BaseUrl => "https://feeds.arstechnica.com";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/arstechnica/index",
                $"{BaseUrl}/arstechnica/technology-lab",
                $"{BaseUrl}/arstechnica/science",
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
