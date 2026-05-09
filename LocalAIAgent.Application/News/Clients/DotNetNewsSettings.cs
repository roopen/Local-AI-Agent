using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News.Clients
{
    internal class DotNetNewsSettings : BaseNewsClientSettings
    {
        public override string ClientName => "DotNetNewsClient";
        public override string DisplayName => ".NET Blog";
        public override string BaseUrl => "https://devblogs.microsoft.com/dotnet/feed/";


        public override string Language => "en";
        public override List<string> GetNewsUrls()
        {
            return new List<string> { BaseUrl };
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
