using LocalAIAgent.SemanticKernel.Chat;
using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.SemanticKernel.News
{
    public interface INewsService
    {
        Task<List<NewsItem>> GetNewsAsync();
    }

    internal class NewsService(
        IHttpClientFactory httpClientFactory,
        IEnumerable<BaseNewsClientSettings> newsClientSettingsList,
        ChatContext chatContext) : INewsService
    {
        private readonly List<NewsItem> newsCache = [];

        public async Task<List<NewsItem>> GetNewsAsync()
        {
            if (newsCache.Count is 0) await LoadAllNews();

            return newsCache;
        }

        internal async Task<int> LoadAllNews()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int feedCount = 0;

            foreach (BaseNewsClientSettings settings in newsClientSettingsList)
            {
                using HttpClient httpClient = httpClientFactory.CreateClient(settings.ClientName);

                foreach (string url in settings.GetNewsUrls())
                {
                    SyndicationFeed feed = await GetNews(httpClient, url);

                    FilterNewsArticles(feed);

                    SaveToVectorDatabaseAsync(feed);

                    feedCount = feed.Items.Count();
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"NewsService: Loaded all news in {stopwatch.ElapsedMilliseconds} ms.");

            return feedCount;
        }

        private void SaveToVectorDatabaseAsync(SyndicationFeed feed)
        {
            foreach (SyndicationItem? item in feed.Items)
            {
                if (item is not null) newsCache.Add(new NewsItem(item));
            }
        }

        /// <summary>
        /// Filters news articles based on user preferences. Reduces the amount of data main chat has to process.
        /// </summary>
        private void FilterNewsArticles(SyndicationFeed feed)
        {
            feed.Items = feed.Items.Where(item => chatContext.IsArticleRelevant(item)).ToList();
        }

        private static async Task<SyndicationFeed> GetNews(HttpClient newsClient, string url)
        {
            Console.WriteLine($"NewsService: GetNews called with url: {newsClient.BaseAddress + url}");
            using Stream stream = await newsClient.GetStreamAsync(url);
            using XmlReader reader = XmlReader.Create(stream);
            SyndicationFeed feed = SyndicationFeed.Load(reader);
            return feed;
        }
    }
}
