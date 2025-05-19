using LocalAIAgent.App.News;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LocalAIAgent.App.RAG
{
    internal partial class RAGService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly InMemoryVectorStoreRecordCollection<string, GenericVectorData> _generalVectorStore;
        private readonly InMemoryVectorStoreRecordCollection<string, NewsItem> _newsVectorStore;

        public RAGService(IEmbeddingGenerator<string, Embedding<float>> embeddingService)
        {
            _embeddingService = embeddingService;

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
        }

        /// <summary>
        /// Saves a news item to the vector store.
        /// </summary>
        /// <returns>Returns database key/id for the item.</returns>
        public async Task<string> SaveNewsAsync(NewsItem newsItem)
        {
            if (newsItem.Content is null) return string.Empty;

            newsItem.Vector = await _embeddingService.GenerateVectorAsync(newsItem.Content);

            return await _newsVectorStore.UpsertAsync(newsItem);
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

        public async Task<string> SearchAsync(string query, int top = 1)
        {
            Console.WriteLine($"RAGPlugin: SearchAsync called (query: {query})");
            await FillVectorDb();

            ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(query);

            VectorSearchOptions<GenericVectorData> options = new()
            {
                VectorProperty = x => x.SimilarityVector,
            };

            IAsyncEnumerable<VectorSearchResult<GenericVectorData>> searchResults = _generalVectorStore.SearchEmbeddingAsync(embedding, top, options);

            string response = string.Empty;
            await foreach (VectorSearchResult<GenericVectorData> result in searchResults)
            {
                Console.WriteLine($"RAGPlugin: found: {result.Record.Chunk}");
                return response += " " + result.Record.Chunk;
            }

            Console.WriteLine($"RAGPlugin: found nothing.");
            return response;
        }

        private async Task FillVectorDb()
        {
            string text = "LOVE & GOOD";

            await _generalVectorStore.UpsertAsync(new GenericVectorData { Chunk = text, Vector = await _embeddingService.GenerateVectorAsync(text) });

            string text2 = "HATE & EVIL";

            await _generalVectorStore.UpsertAsync(new GenericVectorData { Chunk = text2, Vector = await _embeddingService.GenerateVectorAsync(text2) });
        }

        public async Task<string> FilterAsync(string query, int top = 1)
        {
            Console.WriteLine($"RAGPlugin: FilterAsync called (query: {query})");
            await FillVectorDb();

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
                return response += " " + result.Record.Chunk;
            }

            Console.WriteLine($"RAGPlugin: found nothing.");
            return response;
        }

        public async Task<List<string>> FilterNewsAsync(List<string> dislikes, int top = 1)
        {
            Dictionary<string, string> newsSummariesById = [];

            foreach (string dislike in dislikes)
            {
                Console.WriteLine($"RAGPlugin FilterNewsAsync - query: {dislike}");

                ReadOnlyMemory<float> embedding = await _embeddingService.GenerateVectorAsync(dislike);

                VectorSearchOptions<NewsItem> options = new()
                {
                    VectorProperty = x => x.DifferenceVector,
                };

                IAsyncEnumerable<VectorSearchResult<NewsItem>> searchResults = _newsVectorStore.SearchEmbeddingAsync(embedding, 1, options);

                await foreach (VectorSearchResult<NewsItem> result in searchResults)
                {
                    newsSummariesById.TryAdd(result.Record.Id, result.Record.ToString());
                }
            }

            Console.WriteLine($"RAGPlugin: found: {newsSummariesById.Values.Count} news articles.");
            return newsSummariesById.Values.Take(top).ToList();
        }

        private string ReadPdfContentAsText(string path)
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
