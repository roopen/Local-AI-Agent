using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LocalAIAgent.App.RAG.Embedding
{
    internal class EmbeddingService(IOptions<EmbeddingOptions> embeddingOptions, IHttpClientFactory httpClientFactory)
        : IEmbeddingGenerator<string, Embedding<float>>
    {
        private bool disposedValue;

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options,
            CancellationToken cancellationToken)
        {
            EmbeddingResponse embeddingResponse = await GetEmbeddingAsync(values);

            GeneratedEmbeddings<Embedding<float>> result = [];
            foreach (EmbeddingData item in embeddingResponse.Data)
            {
                result.Add(new Embedding<float>(item.Embedding));
            }

            return result;
        }

        public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string query)
        {
            EmbeddingResponse embeddingResponse = await GetEmbeddingAsync([query]);

            return embeddingResponse.Data.First().Embedding;
        }

        private async Task<EmbeddingResponse> GetEmbeddingAsync(IEnumerable<string> values)
        {
            using HttpClient httpClient = httpClientFactory.CreateClient();
            EmbeddingRequest request = new()
            {
                Model = embeddingOptions.Value.ModelId,
                Input = values.ToList()
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(embeddingOptions.Value.EndpointUrl, request);

            return await response.Content.ReadFromJsonAsync<EmbeddingResponse>()
                ?? throw new InvalidOperationException("Failed to deserialize the response to a float array.");
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