using System.ClientModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LocalAIAgent.Application.Chat;

namespace LocalAIAgent.Tests.UseCaseTests;

public class LlmRuntimeManagerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DatasetCollectionUsesTheLlmOption(bool enabled)
    {
        using LlmRuntimeManager runtime = new();
        LlmRuntimeSnapshot candidate = runtime.CreateCandidate(new LlmConnectionSettings(
            "model", "http://localhost:1234/v1/", "", 0.2m, 1m, 0m, 0m, enabled));
        Assert.Equal(enabled, candidate.Options.UseResultsForDataset);
        runtime.Discard(candidate);
    }

    [Fact]
    public void SanitizerExplainsWrappedDnsFailures()
    {
        HttpRequestException dnsFailure = new(
            HttpRequestError.NameResolutionError,
            "The remote name could not be resolved.");
        ClientResultException clientFailure = new("Request failed", null!, dnsFailure);

        string message = LlmErrorSanitizer.GetSafeMessage(clientFailure);

        Assert.Equal(
            "The AI service host could not be resolved. Check the endpoint URL and restart any temporary tunnel.",
            message);
    }

    [Fact]
    public void SanitizerExplainsWrappedConnectionFailures()
    {
        HttpRequestException connectionFailure = new(
            HttpRequestError.ConnectionError,
            "The target machine refused the connection.");
        ClientResultException clientFailure = new("Request failed", null!, connectionFailure);

        string message = LlmErrorSanitizer.GetSafeMessage(clientFailure);

        Assert.Equal(
            "A connection to the AI service could not be established. Check that the service or tunnel is running.",
            message);
    }

    [Fact]
    public void ConnectionFailureClassifierRecognizesWrappedTransportErrors()
    {
        HttpRequestException transportFailure = new(
            HttpRequestError.ConnectionError,
            "The target machine refused the connection.");
        ClientResultException clientFailure = new("Request failed", null!, transportFailure);
        InvalidOperationException wrapper = new("News stream failed", clientFailure);

        Assert.True(LlmErrorSanitizer.IsLlmConnectionFailure(wrapper));
    }

    [Fact]
    public void ConnectionFailureClassifierIgnoresNonLlmProcessingErrors()
    {
        FormatException processingFailure = new("The model response was malformed.");

        Assert.False(LlmErrorSanitizer.IsLlmConnectionFailure(processingFailure));
        Assert.False(LlmErrorSanitizer.IsLlmConnectionFailure(new HttpRequestException(
            HttpRequestError.ConnectionError,
            "A non-LLM HTTP dependency failed.")));
    }

    [Fact]
    public async Task WarmupUsesChatEndpointAndBearerAuthorizationHeader()
    {
        await using OpenAiStubServer server = new();
        using LlmRuntimeManager runtime = new();
        LlmRuntimeSnapshot candidate = runtime.CreateCandidate(new LlmConnectionSettings(
            "test-model",
            server.Endpoint,
            "unsloth-token",
            0.2m,
            1m,
            0m,
            0m));

        await runtime.WarmUpAsync(candidate, TestContext.Current.CancellationToken);
        CapturedRequest request = await server.Request;

        Assert.Equal("POST /v1/chat/completions HTTP/1.1", request.RequestLine);
        Assert.Equal("Bearer unsloth-token", request.Authorization);
        Assert.Matches("\\\"max_(completion_)?tokens\\\":1", request.Body);
        Assert.Contains("\"content\":\"hi\"", request.Body);
        runtime.Discard(candidate);
    }

    [Fact]
    public async Task EmptyTokenCanWarmUpAgainstUnsecuredLocalApi()
    {
        await using OpenAiStubServer server = new();
        using LlmRuntimeManager runtime = new();
        LlmRuntimeSnapshot candidate = runtime.CreateCandidate(new LlmConnectionSettings(
            "test-model",
            server.Endpoint,
            string.Empty,
            0.2m,
            1m,
            0m,
            0m));

        await runtime.WarmUpAsync(candidate, TestContext.Current.CancellationToken);
        CapturedRequest request = await server.Request;

        Assert.Equal("POST /v1/chat/completions HTTP/1.1", request.RequestLine);
        Assert.DoesNotContain("unsloth-token", request.Authorization ?? string.Empty);
        runtime.Discard(candidate);
    }

    private sealed record CapturedRequest(string RequestLine, string? Authorization, string Body);

    private sealed class OpenAiStubServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task<CapturedRequest> _request;

        public OpenAiStubServer()
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Endpoint = $"http://127.0.0.1:{port}/v1/";
            _request = HandleRequestAsync();
        }

        public string Endpoint { get; }
        public Task<CapturedRequest> Request => _request;

        private async Task<CapturedRequest> HandleRequestAsync()
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            string requestLine = await reader.ReadLineAsync() ?? string.Empty;
            int contentLength = 0;
            string? authorization = null;

            while (await reader.ReadLineAsync() is { Length: > 0 } header)
            {
                int separator = header.IndexOf(':');
                if (separator < 0) continue;
                string name = header[..separator];
                string value = header[(separator + 1)..].Trim();
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    authorization = value;
            }

            char[] bodyBuffer = new char[contentLength];
            int read = 0;
            while (read < bodyBuffer.Length)
            {
                int count = await reader.ReadAsync(bodyBuffer.AsMemory(read));
                if (count == 0) break;
                read += count;
            }

            const string responseBody = """
                {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"test-model","choices":[{"index":0,"message":{"role":"assistant","content":"h"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
                """;
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBody);
            string headers = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {responseBytes.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
            await stream.WriteAsync(responseBytes);
            await stream.FlushAsync();

            return new CapturedRequest(requestLine, authorization, new string(bodyBuffer, 0, read));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _request;
            }
            catch (Exception) when (!_request.IsCompletedSuccessfully)
            {
            }
        }
    }
}
