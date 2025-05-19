using LocalAIAgent.App.Chat;
using LocalAIAgent.App.Storage;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System.ComponentModel;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.App.News
{
    internal class NewsService(
        IHttpClientFactory httpClientFactory,
        EmbeddingService embeddingService,
        IEnumerable<INewsClientSettings> newsClientSettingsList,
        ChatContext chatContext)
    {
        private List<string> cachedNews = [];

        [KernelFunction, Description(
            "Get summaries of latest news from a specified source (Yle, Fox, Yahoo).")]
        public async Task<IEnumerable<string>> GetNewsBySourceAsync(string source)
        {
            Console.WriteLine($"NewsService: GetNewsBySourceAsync called (source requested: {source})");
            if (cachedNews.Count is 0)
            {
                IEnumerable<string> news = await GetAllNewsAsync();

                cachedNews = news.ToList();
            }

            return cachedNews.Where(item => item.Contains(source, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<IEnumerable<string>> GetAllNewsAsync()
        {
            Console.WriteLine("NewsService: GetAllNewsAsync called");
            List<string> newsList = [];

            foreach (INewsClientSettings settings in newsClientSettingsList)
            {
                using HttpClient httpClient = httpClientFactory.CreateClient(settings.ClientName);

                foreach (string url in settings.GetNewsUrls())
                {
                    SyndicationFeed feed = await GetNews(httpClient, url);
                    FilterNewsArticles(feed);

                    newsList.AddRange(feed.Items.Select(item => new NewsItem(item).ToString()));

                    await SaveToVectorDatabaseAsync(feed);
                }
            }

            return newsList;
        }

        private async Task SaveToVectorDatabaseAsync(SyndicationFeed feed)
        {
            foreach (SyndicationItem? item in feed.Items)
            {
                NewsItem newsItem = new(item);
                newsItem.Embedding = await embeddingService.GenerateEmbeddingAsync(newsItem);

                InMemoryVectorStoreRecordCollection<string, NewsItem> collection = new("news");
                await collection.CreateCollectionIfNotExistsAsync();
                await collection.UpsertAsync(newsItem);
                NewsItem? record = await collection.GetAsync(newsItem.Id);
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
