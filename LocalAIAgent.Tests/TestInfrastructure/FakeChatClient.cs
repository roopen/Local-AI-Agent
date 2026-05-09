using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.TestInfrastructure;

/// <summary>
/// In-memory IChatClient that returns queued canned responses and records every call.
/// Use Enqueue* before exercising code under test; assert on <see cref="Calls"/> afterwards.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly Queue<ChatResponseUpdate[]> _streamingQueue = new();
    private readonly Queue<ChatResponse> _responseQueue = new();
    private readonly List<RecordedCall> _calls = [];

    public IReadOnlyList<RecordedCall> Calls => _calls;

    /// <summary>Queue a streaming response composed of pre-built updates.</summary>
    public void EnqueueStreaming(params ChatResponseUpdate[] updates) =>
        _streamingQueue.Enqueue(updates);

    /// <summary>Queue a streaming response that emits a single update with the given text.</summary>
    public void EnqueueStreamingText(string text) =>
        _streamingQueue.Enqueue([new ChatResponseUpdate(ChatRole.Assistant, text)]);

    /// <summary>Queue a non-streaming response.</summary>
    public void EnqueueResponse(ChatResponse response) =>
        _responseQueue.Enqueue(response);

    /// <summary>Queue a non-streaming response that contains a single assistant text message.</summary>
    public void EnqueueResponseText(string text) =>
        _responseQueue.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _calls.Add(new RecordedCall([.. messages], options, Streaming: false));

        if (_responseQueue.Count == 0)
            throw new InvalidOperationException(
                $"FakeChatClient.GetResponseAsync was called but no response was queued. Calls so far: {_calls.Count}.");

        return Task.FromResult(_responseQueue.Dequeue());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _calls.Add(new RecordedCall([.. messages], options, Streaming: true));

        if (_streamingQueue.Count == 0)
            throw new InvalidOperationException(
                $"FakeChatClient.GetStreamingResponseAsync was called but no streaming response was queued. Calls so far: {_calls.Count}.");

        ChatResponseUpdate[] updates = _streamingQueue.Dequeue();
        foreach (ChatResponseUpdate update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
            await Task.Yield();
        }
    }

    public void Dispose() { }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(FakeChatClient) ? this : null;
}

public sealed record RecordedCall(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options, bool Streaming);
