using LocalAIAgent.App.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LocalAIAgent.App
{
    internal class EmbeddingService(IOptions<EmbeddingOptions> options, IHttpClientFactory httpClientFactory)
    {
        public async Task<List<float[]>> GenerateEmbeddingAsync(List<string> text)
        {
            HttpClient httpClient = httpClientFactory.CreateClient();
            EmbeddingRequest request = new()
            {
                Model = options.Value.ModelId,
                Input = text
            };

            HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.Value.EndpointUrl, request);
            EmbeddingResponse? embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();

            if (embeddingResponse is null)
                throw new InvalidOperationException("Failed to deserialize the response to a float array.");

            return embeddingResponse.Data.Select(x => x.Embedding).ToList();
        }
    }

    internal class EmbeddingRequest
    {
        public required string Model { get; set; }
        public required List<string> Input { get; set; }

    }

    internal class EmbeddingResponse
    {
        public string Object { get; set; }
        public EmbeddingData[] Data { get; set; }
        public string Model { get; set; }
        public Usage Usage { get; set; }
    }

    internal class Usage
    {
        public int Prompt_tokens { get; set; }
        public int Total_tokens { get; set; }
    }

    internal class EmbeddingData
    {
        public string Object { get; set; }
        public float[] Embedding { get; set; }
        public int Index { get; set; }
    }
}