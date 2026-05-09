using Microsoft.Extensions.Logging;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.Application.News
{
    internal sealed class CustomFeedFetcher(
        IHttpClientFactory httpClientFactory,
        ICustomFeedRepository customFeedRepository,
        ILogger<CustomFeedFetcher> logger) : ICustomFeedFetcher
    {
        private const string ClientName = "CustomFeedClient";
        private const string SourceTagPrefix = "custom:";

        public async Task<List<NewsItem>> FetchAsync(IEnumerable<CustomFeedDescriptor> enabledFeeds, CancellationToken cancellationToken = default)
        {
            // Fetch each feed in parallel, capturing failures per-feed without aborting the batch.
            List<Task<List<NewsItem>>> tasks = [.. enabledFeeds.Select(feed => FetchOneAsync(feed, cancellationToken))];
            List<NewsItem>[] results = await Task.WhenAll(tasks);
            return [.. results.SelectMany(r => r)];
        }

        private async Task<List<NewsItem>> FetchOneAsync(CustomFeedDescriptor feed, CancellationToken cancellationToken)
        {
            HttpClient httpClient = httpClientFactory.CreateClient(ClientName);

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, feed.Url);
                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed parsed = SyndicationFeed.Load(reader);

                string sourceTag = SourceTagPrefix + feed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                List<NewsItem> items = [.. parsed.Items
                    .Where(i => i is not null)
                    .Select(i => new NewsItem(i, sourceTag, feed.Language))];

                await customFeedRepository.RecordFetchResultAsync(feed.Id, errorMessage: null, cancellationToken);
                return items;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CustomFeedFetcher: failed to fetch {Url}", feed.Url);
                await customFeedRepository.RecordFetchResultAsync(feed.Id, ex.Message, cancellationToken);
                return [];
            }
        }
    }
}
