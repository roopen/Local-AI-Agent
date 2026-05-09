using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace LocalAIAgent.Tests.UseCaseTests;

public class LoadLLMUseCaseTests
{
    private const string EnvTokenVar = "LM_API_TOKEN";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(request);
            Bodies.Add(body);
            return new HttpResponseMessage(StatusCode);
        }
    }

    private static AIOptions MakeOptions() => new()
    {
        ModelId = "test-model",
        EndpointUrl = "http://localhost:1234/v1/",
    };

    private static IConfiguration MakeConfig(string? endpointUrl = "http://localhost:1234/v1/", string? modelId = "test-model")
    {
        Dictionary<string, string?> dict = [];
        if (endpointUrl is not null) dict["AIOptions:EndpointUrl"] = endpointUrl;
        if (modelId is not null) dict["AIOptions:ModelId"] = modelId;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task LoadLLMUseCase_ModelAlreadyResponsive_SkipsHttpLoad()
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("pong");

        RecordingHandler handler = new();
        using HttpClient http = new(handler);

        LoadLLMUseCase sut = new(MakeConfig(), MakeOptions(), http, chat);

        bool result = await sut.LoadLLMUseCaseAsync();

        Assert.True(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LoadLLMUseCase_ProbeFails_PostsLoadRequestWithSnakeCaseBody()
    {
        FakeChatClient chat = new();
        // No response queued → IsModelResponsive throws → caught → returns false → HTTP load is attempted.

        RecordingHandler handler = new();
        using HttpClient http = new(handler);

        LoadLLMUseCase sut = new(MakeConfig(), MakeOptions(), http, chat);

        bool result = await sut.LoadLLMUseCaseAsync();

        Assert.True(result);
        HttpRequestMessage req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.EndsWith("/api/v1/models/load", req.RequestUri!.AbsolutePath);

        string body = handler.Bodies[0];
        Assert.Contains("\"model\":\"test-model\"", body);
        Assert.Contains("\"context_length\":", body);
        Assert.Contains("\"flash_attention\":true", body);
        Assert.Contains("\"echo_load_config\":true", body);
    }

    [Fact]
    public async Task LoadLLMUseCase_BearerTokenEnvVar_IsInjectedAsAuthorizationHeader()
    {
        string? prior = Environment.GetEnvironmentVariable(EnvTokenVar);
        Environment.SetEnvironmentVariable(EnvTokenVar, "secret-token-123");
        try
        {
            FakeChatClient chat = new(); // probe will fail → load runs
            RecordingHandler handler = new();
            using HttpClient http = new(handler);

            LoadLLMUseCase sut = new(MakeConfig(), MakeOptions(), http, chat);

            await sut.LoadLLMUseCaseAsync();

            HttpRequestMessage req = Assert.Single(handler.Requests);
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("secret-token-123", req.Headers.Authorization?.Parameter);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvTokenVar, prior);
        }
    }

    [Fact]
    public async Task LoadLLMUseCase_HttpLoadFails_ReturnsFalse()
    {
        FakeChatClient chat = new(); // probe fails
        RecordingHandler handler = new() { StatusCode = HttpStatusCode.InternalServerError };
        using HttpClient http = new(handler);

        LoadLLMUseCase sut = new(MakeConfig(), MakeOptions(), http, chat);

        bool result = await sut.LoadLLMUseCaseAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task LoadLLMUseCase_MissingEndpointConfig_Throws()
    {
        FakeChatClient chat = new();
        using HttpClient http = new(new RecordingHandler());

        LoadLLMUseCase sut = new(MakeConfig(endpointUrl: null), MakeOptions(), http, chat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadLLMUseCaseAsync());
    }

    [Fact]
    public async Task LoadLLMUseCase_MissingModelConfig_Throws()
    {
        FakeChatClient chat = new();
        using HttpClient http = new(new RecordingHandler());

        LoadLLMUseCase sut = new(MakeConfig(modelId: null), MakeOptions(), http, chat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadLLMUseCaseAsync());
    }
}
