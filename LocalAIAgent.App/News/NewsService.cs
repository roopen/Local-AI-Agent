using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.App.News
{
    internal class NewsService(IHttpClientFactory httpClientFactory, IEnumerable<INewsClientSettings> newsClientSettingsList)
    {
        private List<string> cachedNews = [];

        [KernelFunction, Description(
            "Get summaries of latest news from all sources.")]
        public async Task<IEnumerable<string>> GetNewsAsync()
        {
            Console.WriteLine("NewsService: GetNewsAsync called");

            if (cachedNews.Count is 0)
            {
                IEnumerable<string> news = await GetAllNewsAsync();

                cachedNews = news.ToList();
            }

            return cachedNews;
        }

        private async Task<IEnumerable<string>> GetAllNewsAsync()
        {
            Console.WriteLine("NewsService: GetAllNewsAsync called");
            List<string> newsList = [];

            foreach (INewsClientSettings settings in newsClientSettingsList)
            {
                HttpClient httpClient = httpClientFactory.CreateClient(settings.ClientName);

                foreach (string url in settings.GetNewsUrls())
                {
                    SyndicationFeed feed = await GetNews(httpClient, url);
                    newsList.AddRange(feed.Items.Select(item => new NewsItem(item).ToString()));
                }
            }

            return newsList;
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
