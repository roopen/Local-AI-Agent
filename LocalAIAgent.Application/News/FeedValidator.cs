using Microsoft.Extensions.Logging;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.Application.News
{
    internal sealed class FeedValidator(
        IHttpClientFactory httpClientFactory,
        ILogger<FeedValidator> logger) : IFeedValidator
    {
        private const string ClientName = "FeedValidatorClient";

        public async Task<List<FeedUrlValidationResult>> ValidateAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
        {
            HttpClient httpClient = httpClientFactory.CreateClient(ClientName);

            List<Task<FeedUrlValidationResult>> tasks = [.. urls.Select(url => ValidateOneAsync(httpClient, url, cancellationToken))];
            FeedUrlValidationResult[] results = await Task.WhenAll(tasks);
            return [.. results];
        }

        private async Task<FeedUrlValidationResult> ValidateOneAsync(HttpClient httpClient, string url, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new FeedUrlValidationResult(url, false, "Must be an absolute http(s) URL.");
            }

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, uri);
                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new FeedUrlValidationResult(url, false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed parsed = SyndicationFeed.Load(reader);
                _ = parsed.Items.Count(); // force enumeration so a malformed item surfaces here, not at runtime
                return new FeedUrlValidationResult(url, true, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FeedValidator: {Url} failed validation", url);
                return new FeedUrlValidationResult(url, false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
