using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Local_AI_Agent.News
{
    internal class NewsService(IHttpClientFactory httpClientFactory)
    {
        private List<string> cachedNews = [];

        [KernelFunction, Description(
            "Get current summaries of latest news from all sources. " +
            "If this is used, none of the other news functions are required. ")]
        public async Task<IEnumerable<string>> GetNewsAsync()
        {
            Console.WriteLine("NewsService: GetNewsAsync called");

            if (cachedNews.Count is 0)
            {
                IEnumerable<string> newsFromYle = await GetNewsFromYleAsync();
                IEnumerable<string> newsFromFox = await GetNewsFromFoxNewsAsync();

                cachedNews = newsFromYle.Concat(newsFromFox).ToList();
            }

            return cachedNews;
        }

        [KernelFunction, Description("Get current summaries of latest news from the Finnish YLE.")]
        public async Task<IEnumerable<string>> GetNewsFromYleAsync()
        {
            Console.WriteLine("NewsService: GetNewsFromYleAsync called");

            HttpClient httpClient = httpClientFactory.CreateClient(YleNewsSettings.ClientName);
            List<string> newsList = [];

            foreach (string url in YleNewsSettings.GetNewsUrls())
            {
                SyndicationFeed feed = await GetNews(httpClient, url);
                newsList.AddRange(feed.Items.Select(item => new NewsItem(item).ToString()));
            }

            return newsList;
        }

        [KernelFunction, Description("Get current summaries of latest news from the American Fox News.")]
        public async Task<IEnumerable<string>> GetNewsFromFoxNewsAsync()
        {
            Console.WriteLine("NewsService: GetNewsFromFoxNewsAsync called");

            HttpClient httpClient = httpClientFactory.CreateClient(FoxNewsSettings.ClientName);
            List<string> newsList = [];

            foreach (string url in FoxNewsSettings.GetNewsUrls())
            {
                SyndicationFeed feed = await GetNews(httpClient, url);
                newsList.AddRange(feed.Items.Select(item => new NewsItem(item).ToString()));
            }

            return newsList;
        }

        public static async Task<SyndicationFeed> GetNews(HttpClient newsClient, string url)
        {
            Console.WriteLine($"NewsService: GetNews called with url: {newsClient.BaseAddress + url}");
            using Stream stream = await newsClient.GetStreamAsync(url);
            using XmlReader reader = XmlReader.Create(stream);
            SyndicationFeed feed = SyndicationFeed.Load(reader);
            return feed;
        }
    }
}
