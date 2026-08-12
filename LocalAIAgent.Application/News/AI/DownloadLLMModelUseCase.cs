using LocalAIAgent.Application.Chat;
using Serilog;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.Application.News.AI;

public interface IDownloadLLMModelUseCase
{
    Task<bool> DownloadModelAsync(string modelId);
}

internal class DownloadLLMModelUseCase(
    ILlmRuntimeManager runtimeManager,
    HttpClient httpClient) : IDownloadLLMModelUseCase
{
    private sealed record DownloadModelRequest(
        [property: JsonPropertyName("model")] string Model);

    public async Task<bool> DownloadModelAsync(string modelId)
    {
        AIOptions options = runtimeManager.GetRequiredSnapshot().Options;
        Uri downloadUrl = new(new Uri(options.EndpointUrl), "/api/v1/models/download");

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, downloadUrl)
            {
                Content = JsonContent.Create(new DownloadModelRequest(modelId), options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            };

            if (!string.IsNullOrEmpty(options.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            using HttpResponseMessage response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Log.Information("LLM model download started: {ModelId}", modelId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(
                "Failed to download LLM model {ModelId}: {Message}",
                modelId,
                LlmErrorSanitizer.GetSafeMessage(ex));
            return false;
        }
    }
}
