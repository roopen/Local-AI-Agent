using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.App.News
{
    internal interface INewsClientSettings
    {
        string ClientName { get; }

        List<string> GetNewsUrls();

        void AddHttpClient(IServiceCollection services);
    }
}
