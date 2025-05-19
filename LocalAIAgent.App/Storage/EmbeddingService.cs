using LocalAIAgent.App.News;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LocalAIAgent.App.Storage
{
    internal class EmbeddingService(IOptions<EmbeddingOptions> options, IHttpClientFactory httpClientFactory)
        : Microsoft.Extensions.AI.IEmbeddingGenerator
    {
        private bool disposedValue;

        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> text)
        {
            using HttpClient httpClient = httpClientFactory.CreateClient();
            EmbeddingRequest request = new()
            {
                Model = options.Value.ModelId,
                Input = text
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.Value.EndpointUrl, request);
            EmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>()
                ?? throw new InvalidOperationException("Failed to deserialize the response to a float array.");

            return embeddingResponse.Data.Select(x => x.Embedding).ToList();
        }

        public async Task<float[]> GenerateEmbeddingAsync(NewsItem newsItem)
        {
            using HttpClient httpClient = httpClientFactory.CreateClient();
            EmbeddingRequest request = new()
            {
                Model = options.Value.ModelId,
                Input = [newsItem.ToString()]
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.Value.EndpointUrl, request);
            EmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>()
                ?? throw new InvalidOperationException("Failed to deserialize the response to a float array.");

            return embeddingResponse.Data.First().Embedding;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            throw new NotImplementedException();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}