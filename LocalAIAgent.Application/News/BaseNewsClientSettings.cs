using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Application.News
{
    internal abstract class BaseNewsClientSettings
    {
        public abstract string ClientName { get; }
        public abstract string BaseUrl { get; }

        /// <summary>
        /// BCP-47 / ISO 639-1 code of the language the source publishes in
        /// (e.g. "en", "ja", "zh-TW"). Drives the translation decision: if this
        /// differs from the user's target language, articles get translated.
        /// </summary>
        public abstract string Language { get; }

        /// <summary>Human-readable name shown in the Settings UI. Defaults to ClientName minus the "Client" suffix.</summary>
        public virtual string DisplayName => ClientName.Replace("Client", string.Empty);

        public virtual string UserAgent => "Mozilla/5.0";
        public string Host => GetHostFromBaseUrl();
        public virtual List<string> AdditionalHosts => [];

        private string GetHostFromBaseUrl()
        {
            Uri uri = new(BaseUrl);
            return uri.DnsSafeHost;
        }

        public abstract List<string> GetNewsUrls();

        public abstract void AddHttpClient(IServiceCollection services);
    }
}
