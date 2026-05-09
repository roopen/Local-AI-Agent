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
            string sourceTag = SourceTagPrefix + feed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // A feed entry can hold multiple URLs (e.g. several Bloomberg sub-feeds).
            // Fetch them in parallel; record an error if ANY URL fails so the user can fix it.
            List<Task<(List<NewsItem> Items, string? Error)>> urlTasks =
                [.. feed.Urls.Select(url => FetchUrlAsync(httpClient, url, sourceTag, feed.Language, cancellationToken))];
            (List<NewsItem> Items, string? Error)[] urlResults = await Task.WhenAll(urlTasks);

            List<NewsItem> aggregated = [.. urlResults.SelectMany(r => r.Items)];
            string? combinedError = string.Join("; ", urlResults.Where(r => r.Error is not null).Select(r => r.Error));
            if (string.IsNullOrEmpty(combinedError)) combinedError = null;

            await customFeedRepository.RecordFetchResultAsync(feed.Id, combinedError, cancellationToken);
            return aggregated;
        }

        private async Task<(List<NewsItem> Items, string? Error)> FetchUrlAsync(
            HttpClient httpClient, string url, string sourceTag, string language, CancellationToken cancellationToken)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed parsed = SyndicationFeed.Load(reader);

                List<NewsItem> items = [.. parsed.Items
                    .Where(i => i is not null)
                    .Select(i => new NewsItem(i, sourceTag, language))];
                return (items, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CustomFeedFetcher: failed to fetch {Url}", url);
                return ([], $"{url}: {ex.Message}");
            }
        }
    }
}
