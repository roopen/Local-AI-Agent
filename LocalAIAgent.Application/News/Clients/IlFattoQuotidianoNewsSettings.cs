using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class IlFattoQuotidianoNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "IlFattoQuotidianoClient";
        public override string DisplayName => "Il Fatto Quotidiano";
        public override string BaseUrl => "https://www.ilfattoquotidiano.it";
        public override string Language => "it";
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