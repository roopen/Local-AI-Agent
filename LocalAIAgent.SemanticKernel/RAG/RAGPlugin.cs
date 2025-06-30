using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System.ComponentModel;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LocalAIAgent.SemanticKernel.RAG
{
    internal partial class RAGService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly ChatContext _chatContext;
        private readonly INewsService _newsService;
        private readonly InMemoryVectorStoreRecordCollection<string, GenericVectorData> _generalVectorStore;
        private readonly InMemoryVectorStoreRecordCollection<string, NewsItem> _newsVectorStore;
        private readonly Dictionary<string, ReadOnlyMemory<float>> userDislikeVectors = [];

        public RAGService(IEmbeddingGenerator<string, Embedding<float>> embeddingService, ChatContext chatContext, INewsService newsService)
        {
            _embeddingService = embeddingService;
            _chatContext = chatContext;
            _newsService = newsService;

            InMemoryVectorStoreRecordCollectionOptions<string, GenericVectorData> options = new()
            {
                EmbeddingGenerator = _embeddingService,
            };
            _generalVectorStore = new("Docs", options);

            InMemoryVectorStoreRecordCollectionOptions<string, NewsItem> optionsNews = new()
            {
                EmbeddingGenerator = _embeddingService,
            };
            _newsVectorStore = new("News", optionsNews);

            _ = Initialize();
        }

        private async Task Initialize()
        {
            await _generalVectorStore.CreateCollectionIfNotExistsAsync();
            await _newsVectorStore.CreateCollectionIfNotExistsAsync();
            List<NewsItem> news = await _newsService.GetNewsAsync();
            foreach (NewsItem newsItem in news)
            {
                if (newsItem.Content is not null)
                {
                    await SaveNewsAsync(newsItem);
                }
            }
        }

        private async Task LoadUserDislikeVectorsAsync()
        {
            if (userDislikeVectors.Count is 0)
            {
                foreach (string dislike in _chatContext.UserDislikes)
                {
                    ReadOnlyMemory<float> dislikeVector = await _embeddingService.GenerateVectorAsync("I don't want content related to " + dislike);
                    userDislikeVectors.TryAdd(dislike, dislikeVector);
                }
            }
        }

        /// <summary>
        /// Saves a news item to the vector store.
        /// </summary>
        /// <returns>Returns database key/id for the item.</returns>
        public async Task<string> SaveNewsAsync(NewsItem newsItem)
        {
            if (newsItem.Content is null) return string.Empty;

            newsItem.Vector = await _embeddingService.GenerateVectorAsync(newsItem.Content);

            if (userDislikeVectors.Count is 0)
                await LoadUserDislikeVectorsAsync();

            if (!NewsIsFilteredByUserPreferences(newsItem))
            {
                return await _newsVectorStore.UpsertAsync(newsItem);
            }

            return string.Empty;
        }

        private bool NewsIsFilteredByUserPreferences(NewsItem newsItem)
        {
            const float threshold = 0.90f;

            foreach ((string dislike, ReadOnlyMemory<float> dislikeVector) in userDislikeVectors)
            {
                float similarity = CosineSimilarity(newsItem.Vector, dislikeVector);
                if (similarity > threshold)
                {
                    Console.WriteLine($"RAGPlugin: News item violates user dislike {dislike} " +
                        $"with the following content: {newsItem.Content}");
                    return true;
                }
            }

            return false;
        }

        private static float CosineSimilarity(ReadOnlyMemory<float> vectorA, ReadOnlyMemory<float> vectorB)
        {
            ReadOnlySpan<float> spanA = vectorA.Span;
            ReadOnlySpan<float> spanB = vectorB.Span;

            float dotProduct = 0, normA = 0, normB = 0;

            for (int i = 0; i < spanA.Length; i++)
            {
                dotProduct += spanA[i] * spanB[i];
                normA += spanA[i] * spanA[i];
                normB += spanB[i] * spanB[i];
            }

            return dotProduct / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
        }

        /// <summary>
        /// Saves string content to the vector store.
        /// </summary>
        /// <returns>Returns database key/id for the item.</returns>
        public async Task<string> SaveTextAsync(string text)
        {
            GenericVectorData document = new()
            {
                Chunk = text,
                Vector = await _embeddingService.GenerateVectorAsync(text),
            };

            return await _generalVectorStore.UpsertAsync(document);
        }

        [KernelFunction, Description("Search news articles from the RAG.")]
        public async Task<string> SearchAsync(
            [Description("Query (what you want to find)")] string query,
            [Description("Number of articles to return.")] int top = 10)
        {
            Console.WriteLine($"RAGPlugin: SearchAsync called (query: {query})");

            ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(query);

            VectorSearchOptions<NewsItem> options = new()
            {
                VectorProperty = x => x.SimilarityVector,
            };

            IAsyncEnumerable<VectorSearchResult<NewsItem>> searchResults = _newsVectorStore.SearchEmbeddingAsync(embedding, top, options);

            string response = string.Empty;
            await foreach (VectorSearchResult<NewsItem> result in searchResults)
            {
                Console.WriteLine($"RAGPlugin: found: {result.Record.Content}");
                response += " " + result.Record.Content;
            }

            return response;
        }

        [KernelFunction, Description("Get news articles by filtering.")]
        public async Task<string> FilterAsync(
            [Description("Query (what you want to avoid)")] string query,
            [Description("Number of articles to return.")] int top = 10)
        {
            Console.WriteLine($"RAGPlugin: FilterAsync called (query: {query})");

            ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(query);

            VectorSearchOptions<GenericVectorData> options = new()
            {
                VectorProperty = x => x.DifferenceVector,
            };

            IAsyncEnumerable<VectorSearchResult<GenericVectorData>> searchResults = _generalVectorStore.SearchEmbeddingAsync(embedding, top, options);

            string response = string.Empty;
            await foreach (VectorSearchResult<GenericVectorData> result in searchResults)
            {
                Console.WriteLine($"RAGPlugin: found: {result.Record.Chunk}");
                response += " " + result.Record.Chunk;
            }

            return response;
        }

        public async Task<List<string>> FilterNewsAsync(List<string> dislikes, int top = 1)
        {
            Dictionary<string, string> newsSummariesById = [];
            int topPerDislike = GetTopPerTopic(dislikes, top);

            foreach (string dislike in dislikes)
            {
                ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(dislike);

                VectorSearchOptions<NewsItem> options = new()
                {
                    VectorProperty = x => x.DifferenceVector,
                };

                IAsyncEnumerable<VectorSearchResult<NewsItem>> searchResults = _newsVectorStore.SearchEmbeddingAsync(embedding, topPerDislike, options);

                await foreach (VectorSearchResult<NewsItem> result in searchResults)
                {
                    newsSummariesById.TryAdd(result.Record.Id, result.Record.ToString());
                }
            }

            Console.WriteLine($"RAGPlugin: found: {newsSummariesById.Values.Count} news articles.");
            return newsSummariesById.Values.Take(top).ToList();
        }

        public async Task<List<string>> SearchNewsAsync(List<string> interests, int top = 1)
        {
            Dictionary<string, string> newsSummariesById = [];
            int topPerInterest = GetTopPerTopic(interests, top);

            foreach (string interest in interests)
            {
                ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(interest);

                VectorSearchOptions<NewsItem> options = new()
                {
                    VectorProperty = x => x.SimilarityVector,
                };

                IAsyncEnumerable<VectorSearchResult<NewsItem>> searchResults = _newsVectorStore.SearchEmbeddingAsync(embedding, topPerInterest, options);

                await foreach (VectorSearchResult<NewsItem> result in searchResults)
                {
                    newsSummariesById.TryAdd(result.Record.Id, result.Record.ToString());
                }
            }

            Console.WriteLine($"RAGPlugin: found: {newsSummariesById.Values.Count} news articles.");
            return newsSummariesById.Values.Take(top).ToList();
        }

        private static int GetTopPerTopic(List<string> dislikes, int top)
        {
            if (dislikes.Count > top) return (int)Math.Ceiling(dislikes.Count / (double)top);
            else return (int)Math.Ceiling((double)top / dislikes.Count);
        }

        private static string ReadPdfContentAsText(string path)
        {
            string text = string.Empty;
            using (PdfDocument document = PdfDocument.Open(path))
            {
                foreach (Page page in document.GetPages())
                {
                    text += page.Text + "\n";
                }
            }
            return text;
        }
    }
}
