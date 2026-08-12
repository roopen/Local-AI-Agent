using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.TestInfrastructure;

public sealed class FakeLlmRuntimeManager(AIOptions options, IChatClient chatClient)
    : ILlmRuntimeManager
{
    private LlmRuntimeSnapshot _snapshot = new(options, chatClient);

    public bool IsConfigured { get; set; } = true;
    public int WarmUpCalls { get; private set; }
    public LlmConnectionException? WarmUpException { get; set; }

    public LlmRuntimeSnapshot GetRequiredSnapshot() => IsConfigured
        ? _snapshot
        : throw new InvalidOperationException("LLM API settings are required before AI features can be used.");

    public LlmRuntimeSnapshot CreateCandidate(LlmConnectionSettings settings) => new(
        new AIOptions
        {
            ModelId = settings.ModelId.Trim(),
            EndpointUrl = settings.EndpointUrl,
            ApiKey = settings.ApiKey,
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            FrequencyPenalty = settings.FrequencyPenalty,
            PresencePenalty = settings.PresencePenalty,
            UseResultsForDataset = _snapshot.Options.UseResultsForDataset,
        },
        _snapshot.ChatClient);

    public async Task WarmUpAsync(
        LlmRuntimeSnapshot candidate,
        CancellationToken cancellationToken = default)
    {
        WarmUpCalls++;
        if (WarmUpException is not null)
            throw WarmUpException;

        List<ChatMessage> messages = [new ChatMessage(ChatRole.User, "hi")];
        await candidate.ChatClient.GetResponseAsync(
            messages,
            new ChatOptions { MaxOutputTokens = 1 },
            cancellationToken);
    }

    public void Activate(LlmRuntimeSnapshot candidate)
    {
        _snapshot = candidate;
        IsConfigured = true;
    }

    public void Discard(LlmRuntimeSnapshot candidate)
    {
    }
}
