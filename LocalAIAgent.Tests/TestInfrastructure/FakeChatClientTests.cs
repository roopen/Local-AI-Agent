using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.TestInfrastructure;

public class FakeChatClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetResponseAsync_ReturnsQueuedResponseInOrder()
    {
        FakeChatClient client = new();
        client.EnqueueResponseText("first");
        client.EnqueueResponseText("second");

        ChatResponse a = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "ping")], cancellationToken: Ct);
        ChatResponse b = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "pong")], cancellationToken: Ct);

        Assert.Equal("first", a.Text);
        Assert.Equal("second", b.Text);
    }

    [Fact]
    public async Task GetResponseAsync_NoResponseQueued_Throws()
    {
        FakeChatClient client = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "ping")], cancellationToken: Ct));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsAllUpdatesInOrder()
    {
        FakeChatClient client = new();
        client.EnqueueStreaming(
            new ChatResponseUpdate(ChatRole.Assistant, "Hello "),
            new ChatResponseUpdate(ChatRole.Assistant, "world"));

        List<string> chunks = [];
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: Ct))
        {
            chunks.Add(update.Text ?? string.Empty);
        }

        Assert.Equal(["Hello ", "world"], chunks);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NoneQueued_Throws()
    {
        FakeChatClient client = new();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: Ct))
            {
                // never reached
            }
        });
    }

    [Fact]
    public async Task Calls_AreRecordedWithMessagesAndStreamingFlag()
    {
        FakeChatClient client = new();
        client.EnqueueResponseText("ok");
        client.EnqueueStreamingText("ok");

        ChatOptions options = new() { Temperature = 0.5f };
        await client.GetResponseAsync([new ChatMessage(ChatRole.System, "sys"), new ChatMessage(ChatRole.User, "u")], options, cancellationToken: Ct);
        await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "stream")], cancellationToken: Ct)) { }

        Assert.Equal(2, client.Calls.Count);
        Assert.False(client.Calls[0].Streaming);
        Assert.Equal(2, client.Calls[0].Messages.Count);
        Assert.Same(options, client.Calls[0].Options);

        Assert.True(client.Calls[1].Streaming);
        Assert.Single(client.Calls[1].Messages);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_HonoursCancellation()
    {
        FakeChatClient client = new();
        client.EnqueueStreaming(
            new ChatResponseUpdate(ChatRole.Assistant, "one"),
            new ChatResponseUpdate(ChatRole.Assistant, "two"));

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token))
            {
                // First yield triggers the ThrowIfCancellationRequested check.
            }
        });
    }
}
