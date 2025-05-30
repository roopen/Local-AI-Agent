﻿using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.RAG;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Xml;

namespace LocalAIAgent.SemanticKernel.News
{
    internal class NewsService(
        IHttpClientFactory httpClientFactory,
        IEnumerable<BaseNewsClientSettings> newsClientSettingsList,
        RAGService ragService,
        ChatContext chatContext)
    {
        [KernelFunction, Description("Get the latest news articles from various sources.")]
        public async Task<List<string>> GetNewsAsync()
        {
            List<string> result = await ragService.SearchNewsAsync(chatContext.UserInterests, 30);

            return result;
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

                    await SaveToVectorDatabaseAsync(feed);

                    feedCount = feed.Items.Count();
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"NewsService: Loaded all news in {stopwatch.ElapsedMilliseconds} ms.");

            return feedCount;
        }

        private async Task SaveToVectorDatabaseAsync(SyndicationFeed feed)
        {
            foreach (SyndicationItem? item in feed.Items)
            {
                if (item is not null) await ragService.SaveNewsAsync(new NewsItem(item));
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
