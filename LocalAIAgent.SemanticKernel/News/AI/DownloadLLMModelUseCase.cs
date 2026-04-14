using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.News.AI;

public interface IDownloadLLMModelUseCase
{
    Task<bool> DownloadModelAsync(string modelId);
}

internal class DownloadLLMModelUseCase(
    IConfiguration configuration,
    HttpClient httpClient) : IDownloadLLMModelUseCase
{
    private sealed record DownloadModelRequest(
        [property: JsonPropertyName("model")] string Model);

    public async Task<bool> DownloadModelAsync(string modelId)
    {
        string endpointUrl = configuration["AIOptions:EndpointUrl"]
            ?? throw new InvalidOperationException("AIOptions:EndpointUrl is not configured.");

        Uri downloadUrl = new(new Uri(endpointUrl), "/api/v1/models/download");

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, downloadUrl)
            {
                Content = JsonContent.Create(new DownloadModelRequest(modelId), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            };

            string? apiToken = Environment.GetEnvironmentVariable("LM_API_TOKEN");
            if (!string.IsNullOrEmpty(apiToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Log.Information("LLM model download started: {ModelId}", modelId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to download LLM model {ModelId}", modelId);
            return false;
        }
    }
}
