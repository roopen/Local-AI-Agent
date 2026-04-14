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
        Task<bool> LoadLLMUseCaseAsync(string modelId);
    }

    internal class LoadLLMUseCase(
        Kernel kernel,
        IConfiguration configuration,
        HttpClient httpClient) : ILoadLLMUseCase
    {
        private sealed record LoadModelRequest(
            string Model,
            int ContextLength,
            bool FlashAttention,
            bool EchoLoadConfig);

        public async Task<bool> LoadLLMUseCaseAsync(string modelId)
        {
            string endpointUrl = configuration["AIOptions:EndpointUrl"]
                ?? throw new InvalidOperationException("AIOptions:EndpointUrl is not configured.");
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
                Log.Debug($"LLM loaded by LM Studio: " + await response.Content.ReadAsStringAsync());
                Console.WriteLine($"LLM loaded by LM Studio: " + await response.Content.ReadAsStringAsync());
                return true;
            }
            catch
            {
                try
                {
                    ChatCompletionAgent agent = new()
                    {
                        Kernel = kernel,
                        Arguments = new KernelArguments(new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
                        {
                            ServiceId = "General",
                            ModelId = configuration["AIOptions:ModelId"],
                            FunctionChoiceBehavior = FunctionChoiceBehavior.None(),
                        }),
                    };

                    ChatHistoryAgentThread thread = new();
                    ChatMessageContent message = new(AuthorRole.User, "Hello");
                    ChatMessageContent? response = null;
                    await foreach (ChatMessageContent msg in agent.InvokeAsync(message, thread).ConfigureAwait(false))
                        response = msg;

                    Log.Debug($"LLM Load response: {response}");
                    Console.WriteLine($"LLM Load response: {response}");
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}