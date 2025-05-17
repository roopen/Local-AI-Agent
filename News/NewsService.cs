using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Local_AI_Agent.News
{
    internal class NewsService(IHttpClientFactory httpClientFactory)
    {
        [KernelFunction, Description("Get current summaries of latest news from all sources.")]
        public async Task<IEnumerable<string>> GetNewsAsync()
        {
            IEnumerable<string> newsFromYle = await GetNewsFromYleAsync();
            IEnumerable<string> newsFromFox = await GetNewsFromFoxNewsAsync();

            return newsFromYle.Concat(newsFromFox);
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

    public class NewsItem
    {
        public readonly DateTimeOffset PublishDate;
        public readonly string Title;
        public readonly string? Summary;
        public readonly string? Content;

        public NewsItem(SyndicationItem syndicationItem)
        {
            Title = syndicationItem.Title.Text;
            Content = syndicationItem.Content?.ToString();
            Summary = syndicationItem.Summary?.Text;
            PublishDate = syndicationItem.PublishDate;
        }

        public override string ToString()
        {
            return $"{Title} ({PublishDate})\n{Summary}\n{Content}";
        }
    }
}
