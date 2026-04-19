using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News.Clients
{
    internal class NewScientistNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NewScientistClient";
        public override string BaseUrl => "https://www.newscientist.com";

        public override List<string> GetNewsUrls()
        {
            return
            [
                $"{BaseUrl}/feed/home/",
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
