using LocalAIAgent.Application.Chat;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.Application.News.AI;

public interface IGetLMStudioModelsUseCase
{
    Task<List<LMStudioModel>> GetModelsAsync();
}

internal class GetLMStudioModelsUseCase(
    ILlmRuntimeManager runtimeManager,
    HttpClient httpClient) : IGetLMStudioModelsUseCase
{
    private sealed record ModelsResponse(
        [property: JsonPropertyName("models")] List<LMStudioModel> Models);

    public async Task<List<LMStudioModel>> GetModelsAsync()
    {
        AIOptions options = runtimeManager.GetRequiredSnapshot().Options;
        Uri modelsUrl = new(new Uri(options.EndpointUrl), "/api/v1/models");
        using HttpRequestMessage request = new(HttpMethod.Get, modelsUrl);

        if (!string.IsNullOrEmpty(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using HttpResponseMessage response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        ModelsResponse? result = await response.Content.ReadFromJsonAsync<ModelsResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Models ?? [];
    }
}
