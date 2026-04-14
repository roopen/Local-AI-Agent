using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface ILoadLLMUseCase
    {
        Task<bool> LoadLLMUseCaseAsync();
    }

    internal class LoadLLMUseCase(
        IConfiguration configuration,
        HttpClient httpClient) : ILoadLLMUseCase
    {
        private sealed record LoadModelRequest(
            string Model,
            int ContextLength,
            bool FlashAttention,
            bool EchoLoadConfig);

        public async Task<bool> LoadLLMUseCaseAsync()
        {
            string endpointUrl = configuration["AIOptions:EndpointUrl"]
                ?? throw new InvalidOperationException("AIOptions:EndpointUrl is not configured.");
            string modelId = configuration["AIOptions:ModelId"]
                ?? throw new InvalidOperationException("AIOptions:ModelId is not configured.");
            Uri lmStudioLoadUrl = new(new Uri(endpointUrl), "/api/v1/models/load");

            try
            {
                LoadModelRequest request = new(
                    Model: modelId,
                    ContextLength: 23322,
                    FlashAttention: true,
                    EchoLoadConfig: true);

                using HttpRequestMessage httpRequest = new(HttpMethod.Post, lmStudioLoadUrl)
                {
                    Content = JsonContent.Create(request, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    })
                };

                string? apiToken = Environment.GetEnvironmentVariable("LM_API_TOKEN");
                if (!string.IsNullOrEmpty(apiToken))
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                using HttpResponseMessage response = await httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();
                Log.Information("LLM model loaded: {ModelId}", modelId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load LLM model {ModelId}", modelId);
                return false;
            }
        }
    }
}