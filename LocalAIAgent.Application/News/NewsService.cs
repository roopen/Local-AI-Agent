using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace LocalAIAgent.Application.News
{
    public interface INewsService
    {
        Task<List<NewsItem>> GetNewsAsync();

        /// <summary>
        /// Uses the list of dislikes for keyword filtering.
        /// </summary>
        Task<List<NewsItem>> GetNewsAsync(List<string> dislikes);
    }

    internal class NewsService(
        IHttpClientFactory httpClientFactory,
        IEnumerable<BaseNewsClientSettings> newsClientSettingsList,
        TimeProvider timeProvider,
        ILogger<NewsService> logger) : INewsService
    {
        private List<NewsItem> newsCache = [];

        public async Task<List<NewsItem>> GetNewsAsync()
        {
            if (newsCache.Count is 0) await LoadAllNews();

            return newsCache;
        }

        public async Task<List<NewsItem>> GetNewsAsync(List<string> dislikes)
        {
            if (newsCache.Count is 0) await LoadAllNews();

            DateTimeOffset cutoff = timeProvider.GetUtcNow().AddDays(-1);
            List<NewsItem> filteredNews = FilterNews(newsCache, dislikes, cutoff);

            double filterPercentage = 100 - (filteredNews.Count / (double)newsCache.Count * 100);
            NewsLogging.LogNewsFiltered(logger, newsCache.Count, filteredNews.Count, filterPercentage, null);

            return filteredNews;
        }

        internal async Task<int> LoadAllNews()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<Task<SyndicationFeed>> tasks = newsClientSettingsList
                .Distinct()
                .SelectMany(settings =>
                {
                    HttpClient httpClient = httpClientFactory.CreateClient(settings.ClientName);
                    return settings.GetNewsUrls().Select(url => GetNews(httpClient, url));
                })
                .ToList();

            SyndicationFeed[] feeds = await Task.WhenAll(tasks);

            foreach (SyndicationFeed feed in feeds)
            {
                CacheNewsArticles(feed);
            }

            stopwatch.Stop();
            logger.LogInformation("NewsService: loaded all news in {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);

            return feeds.Sum(f => f.Items.Count());
        }

        private void CacheNewsArticles(SyndicationFeed feed)
        {
            List<NewsItem> newItems = feed.Items
                .Where(item => item != null)
                .Select(item => new NewsItem(item))
                .ToList();

            lock (newsCache)
            {
                newsCache = newsCache
                    .Concat(newItems)
                    .DistinctBy(item => item.Link)
                    .ToList();
            }
        }

        private async Task<SyndicationFeed> GetNews(HttpClient newsClient, string url)
        {
            logger.LogDebug("NewsService: fetching {Url}", newsClient.BaseAddress + url);
            try
            {
                using Stream stream = await newsClient.GetStreamAsync(url);
                using XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                return feed;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "NewsService: failed to fetch news from {Url}", url);
                return new SyndicationFeed();
            }
        }

        internal static List<NewsItem> FilterNews(List<NewsItem> news, List<string> dislikes, DateTimeOffset cutoff)
        {
            return news
                .Where(item => item.PublishDate >= cutoff)
                .Where(item => PassesDislikeFilter(item, dislikes))
                .ToList();
        }

        internal static bool PassesDislikeFilter(NewsItem item, List<string> dislikes)
        {
            if (item is not null)
            {
                return SimpleWordFilter(item, dislikes);
            }

            return false;
        }

        /// <summary>
        /// Filters out articles based on simple word matching.
        /// </summary>
        private static bool SimpleWordFilter(NewsItem item, List<string> dislikes)
        {
            foreach (string dislike in dislikes)
            {
                string pattern = $@"\b{Regex.Escape(dislike)}\b"; // Ensure whole word match

                foreach (string category in item.Categories)
                {
                    if (!string.IsNullOrEmpty(category) && Regex.IsMatch(category, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(item.Title) && Regex.IsMatch(item.Title, pattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }

                if (item.Summary is not null && Regex.IsMatch(item.Summary, pattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
