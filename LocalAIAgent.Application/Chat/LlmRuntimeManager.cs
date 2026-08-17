using System.ClientModel;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Extensions.AI;
using OpenAI;

namespace LocalAIAgent.Application.Chat;

public sealed record LlmConnectionSettings(
    string ModelId,
    string EndpointUrl,
    string ApiKey,
    decimal Temperature,
    decimal TopP,
    decimal FrequencyPenalty,
    decimal PresencePenalty);

public sealed record LlmRuntimeSnapshot(AIOptions Options, IChatClient ChatClient);

public sealed class LlmConnectionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class LlmErrorSanitizer
{
    public const string ConnectionFailureCode = "LLM_CONNECTION_FAILED:";

    public static bool IsLlmConnectionFailure(Exception exception)
    {
        if (exception is AggregateException aggregateException)
            return aggregateException.Flatten().InnerExceptions.Any(IsLlmConnectionFailure);

        if (exception is LlmConnectionException
            or ClientResultException)
        {
            return true;
        }

        if (exception is InvalidOperationException
            && exception.Message.StartsWith("LLM API settings are required", StringComparison.Ordinal))
        {
            return true;
        }

        return exception.InnerException is not null
            && IsLlmConnectionFailure(exception.InnerException);
    }

    public static string GetSafeMessage(Exception exception)
    {
        if (exception is AggregateException aggregateException)
            exception = aggregateException.Flatten().InnerExceptions[0];

        if (exception is LlmConnectionException connectionException)
            return connectionException.Message;

        if (FindException<OperationCanceledException>(exception) is not null)
            return "The AI service request timed out or was cancelled.";

        if (FindException<ClientResultException>(exception) is { } clientException)
        {
            string? responseMessage = clientException.Status switch
            {
                401 or 403 => "The AI service rejected the API token.",
                404 => "The AI endpoint or model was not found.",
                408 => "The AI service request timed out.",
                429 => "The AI service rate limit was reached.",
                >= 500 => $"The AI service is unavailable (HTTP {clientException.Status}).",
                > 0 => $"The AI service rejected the request (HTTP {clientException.Status}).",
                _ => null,
            };

            return responseMessage
                ?? GetTransportMessage(clientException)
                ?? "The AI service request failed before a response was received.";
        }

        if (FindException<TimeoutException>(exception) is not null)
            return "The AI service request timed out.";

        if (GetTransportMessage(exception) is { } transportMessage)
            return transportMessage;

        if (exception is InvalidOperationException
            && exception.Message.StartsWith("LLM API settings are required", StringComparison.Ordinal))
        {
            return exception.Message;
        }

        return "The AI service request failed.";
    }

    private static string? GetTransportMessage(Exception exception)
    {
        if (FindException<HttpRequestException>(exception) is { } requestException)
        {
            return requestException.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError =>
                    "The AI service host could not be resolved. Check the endpoint URL and restart any temporary tunnel.",
                HttpRequestError.ConnectionError =>
                    "A connection to the AI service could not be established. Check that the service or tunnel is running.",
                HttpRequestError.SecureConnectionError =>
                    "A secure connection to the AI service could not be established. Check its HTTPS certificate.",
                _ => "The AI service could not be reached.",
            };
        }

        if (FindException<SocketException>(exception) is { } socketException)
        {
            return socketException.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain =>
                    "The AI service host could not be resolved. Check the endpoint URL and restart any temporary tunnel.",
                _ => "A connection to the AI service could not be established. Check that the service or tunnel is running.",
            };
        }

        if (FindException<AuthenticationException>(exception) is not null)
            return "A secure connection to the AI service could not be established. Check its HTTPS certificate.";

        return null;
    }

    private static TException? FindException<TException>(Exception exception)
        where TException : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException match)
                return match;
        }

        return null;
    }
}

public interface ILlmRuntimeManager
{
    bool IsConfigured { get; }
    LlmRuntimeSnapshot GetRequiredSnapshot();
    LlmRuntimeSnapshot CreateCandidate(LlmConnectionSettings settings);
    Task WarmUpAsync(LlmRuntimeSnapshot candidate, CancellationToken cancellationToken = default);
    void Activate(LlmRuntimeSnapshot candidate);
    void Discard(LlmRuntimeSnapshot candidate);
}

internal sealed class LlmRuntimeManager(AIApplicationOptions applicationOptions)
    : ILlmRuntimeManager, IDisposable
{
    private readonly object _sync = new();
    private readonly HashSet<IChatClient> _ownedClients = [];
    private LlmRuntimeSnapshot? _current;

    public bool IsConfigured => Volatile.Read(ref _current) is not null;

    public LlmRuntimeSnapshot GetRequiredSnapshot() =>
        Volatile.Read(ref _current)
        ?? throw new InvalidOperationException("LLM API settings are required before AI features can be used.");

    public LlmRuntimeSnapshot CreateCandidate(LlmConnectionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelId))
            throw new LlmConnectionException("A model ID is required.");

        if (!Uri.TryCreate(settings.EndpointUrl, UriKind.Absolute, out Uri? endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new LlmConnectionException("The endpoint must be an absolute HTTP or HTTPS URL.");
        }

        AIOptions options = new()
        {
            ModelId = settings.ModelId.Trim(),
            EndpointUrl = endpoint.AbsoluteUri,
            ApiKey = settings.ApiKey,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            FrequencyPenalty = settings.FrequencyPenalty,
            PresencePenalty = settings.PresencePenalty,
            UseResultsForDataset = applicationOptions.UseResultsForDataset,
        };

        OpenAIClientOptions clientOptions = new()
        {
            Endpoint = endpoint,
            NetworkTimeout = TimeSpan.FromSeconds(90),
        };

        string apiKey = string.IsNullOrEmpty(settings.ApiKey) ? "no-key" : settings.ApiKey;
        OpenAIClient openAIClient = new(new ApiKeyCredential(apiKey), clientOptions);
        IChatClient chatClient = openAIClient.GetChatClient(options.ModelId).AsIChatClient();
        return new LlmRuntimeSnapshot(options, chatClient);
    }

    public async Task WarmUpAsync(
        LlmRuntimeSnapshot candidate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<ChatMessage> messages = [new ChatMessage(ChatRole.User, "hi")];
            ChatOptions warmupOptions = new() { MaxOutputTokens = 1 };
            await candidate.ChatClient.GetResponseAsync(messages, warmupOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not LlmConnectionException)
        {
            throw new LlmConnectionException(LlmErrorSanitizer.GetSafeMessage(ex), ex);
        }
    }

    public void Activate(LlmRuntimeSnapshot candidate)
    {
        lock (_sync)
        {
            _ownedClients.Add(candidate.ChatClient);
            Volatile.Write(ref _current, candidate);
        }
    }

    public void Discard(LlmRuntimeSnapshot candidate)
    {
        lock (_sync)
        {
            if (!_ownedClients.Contains(candidate.ChatClient))
                candidate.ChatClient.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (IChatClient client in _ownedClients)
                client.Dispose();

            _ownedClients.Clear();
            Volatile.Write(ref _current, null);
        }
    }
}
