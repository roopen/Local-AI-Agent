using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.News.AI;

public interface IGetLMStudioModelsUseCase
{
    Task<List<LMStudioModel>> GetModelsAsync();
}

internal class GetLMStudioModelsUseCase(
    IConfiguration configuration,
    HttpClient httpClient) : IGetLMStudioModelsUseCase
{
    private sealed record ModelsResponse(
        [property: JsonPropertyName("models")] List<LMStudioModel> Models);

    public async Task<List<LMStudioModel>> GetModelsAsync()
    {
        string endpointUrl = configuration["AIOptions:EndpointUrl"]
            ?? throw new InvalidOperationException("AIOptions:EndpointUrl is not configured.");

        Uri modelsUrl = new(new Uri(endpointUrl), "/api/v1/models");

        using HttpRequestMessage request = new(HttpMethod.Get, modelsUrl);

        string? apiToken = Environment.GetEnvironmentVariable("LM_API_TOKEN");
        if (!string.IsNullOrEmpty(apiToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using HttpResponseMessage response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        ModelsResponse? result = await response.Content.ReadFromJsonAsync<ModelsResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Models ?? [];
    }
}
