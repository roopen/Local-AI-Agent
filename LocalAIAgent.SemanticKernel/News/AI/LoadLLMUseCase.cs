using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
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
        AIOptions options,
        HttpClient httpClient,
        Kernel kernel) : ILoadLLMUseCase
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

            if (await IsModelResponsiveAsync(modelId))
            {
                Log.Information("LLM model {ModelId} is already loaded and responsive, skipping explicit load", modelId);
                return true;
            }

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

        private async Task<bool> IsModelResponsiveAsync(string modelId)
        {
            try
            {
                ChatCompletionAgent agent = new()
                {
                    Kernel = kernel,
                    Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false))
                };

                ChatHistoryAgentThread thread = new();
                ChatMessageContent userMessage = new(AuthorRole.User, "ping");

                await foreach (AgentResponseItem<ChatMessageContent> _ in agent.InvokeAsync(userMessage, thread))
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "LLM model {ModelId} probe failed, will attempt explicit load", modelId);
                return false;
            }
        }
    }
}