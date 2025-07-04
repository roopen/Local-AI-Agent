using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace LocalAIAgent.SemanticKernel.News
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

            List<NewsItem> filteredNews = newsCache.Where(item => PassesDislikeFilter(item, dislikes)).ToList();

            double filterPercentage = 100 - (filteredNews.Count / (double)newsCache.Count * 100);
            NewsLogging.LogNewsFiltered(logger, newsCache.Count, filteredNews.Count, filterPercentage, null);

            return filteredNews;
        }

        internal async Task<int> LoadAllNews()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int feedCount = 0;

            foreach (BaseNewsClientSettings settings in newsClientSettingsList.Distinct())
            {
                using HttpClient httpClient = httpClientFactory.CreateClient(settings.ClientName);

                foreach (string url in settings.GetNewsUrls())
                {
                    SyndicationFeed feed = await GetNews(httpClient, url);

                    CacheNewsArticles(feed);

                    feedCount = feed.Items.Count();
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"NewsService: Loaded all news in {stopwatch.ElapsedMilliseconds} ms.");

            return feedCount;
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

        private static async Task<SyndicationFeed> GetNews(HttpClient newsClient, string url)
        {
            Console.WriteLine($"NewsService: GetNews called with url: {newsClient.BaseAddress + url}");
            try
            {
                using Stream stream = await newsClient.GetStreamAsync(url);
                using XmlReader reader = XmlReader.Create(stream);
                SyndicationFeed feed = SyndicationFeed.Load(reader);
                return feed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NewsService: Error fetching news from {url}: {ex.Message}");
                return new SyndicationFeed();
            }
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
                    if (Regex.IsMatch(category, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }

                if (Regex.IsMatch(item.Title, pattern, RegexOptions.IgnoreCase))
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
