using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.SemanticKernel.News
{
    internal abstract class BaseNewsClientSettings
    {
        public abstract string ClientName { get; }
        public abstract string BaseUrl { get; }
        public virtual string UserAgent => "Mozilla/5.0";
        public string Host => GetHostFromBaseUrl();
        public virtual bool RequiresTranslation => false;

        private string GetHostFromBaseUrl()
        {
            Uri uri = new(BaseUrl);
            return uri.Host;
        }

        public abstract List<string> GetNewsUrls();

        public abstract void AddHttpClient(IServiceCollection services);
    }
}
