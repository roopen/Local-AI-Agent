using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class NewScientistNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "NewScientistClient";
        public override string DisplayName => "New Scientist";
        public override string BaseUrl => "https://www.newscientist.com";


        public override string Language => "en";
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
