using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.TestInfrastructure;

public sealed class FakeLlmRuntimeManager(AIOptions options, IChatClient chatClient)
    : ILlmRuntimeManager
{
    private LlmRuntimeSnapshot _snapshot = new(options, chatClient);
    private readonly Dictionary<int, LlmRuntimeSnapshot> _runtimes = [];
    private readonly Dictionary<int, int> _userSelections = [];

    public bool IsConfigured { get; set; } = true;
    public int WarmUpCalls { get; private set; }
    public LlmConnectionException? WarmUpException { get; set; }

    public bool IsConfiguredFor(int settingsId) =>
        IsConfigured && _runtimes.ContainsKey(settingsId);

    public LlmRuntimeSnapshot GetRequiredSnapshot(int? userPreferencesId = null)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "LLM API settings are required before AI features can be used.");
        }

        if (userPreferencesId is int preferencesId
            && _userSelections.TryGetValue(preferencesId, out int settingsId)
            && _runtimes.TryGetValue(settingsId, out LlmRuntimeSnapshot? selected))
        {
            return selected;
        }

        return _runtimes.Count > 0
            ? _runtimes.OrderBy(pair => pair.Key).First().Value
            : _snapshot;
    }

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
            UseResultsForDataset = settings.UseResultsForDataset,
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

    public void Activate(int settingsId, LlmRuntimeSnapshot candidate)
    {
        _runtimes[settingsId] = candidate;
        _snapshot = candidate;
        IsConfigured = true;
    }

    public void Remove(int settingsId)
    {
        _runtimes.Remove(settingsId);
        foreach (int preferencesId in _userSelections
            .Where(pair => pair.Value == settingsId)
            .Select(pair => pair.Key)
            .ToList())
        {
            _userSelections.Remove(preferencesId);
        }

        IsConfigured = _runtimes.Count > 0;
    }

    public void SetUserSelection(int userPreferencesId, int settingsId)
    {
        if (!IsConfiguredFor(settingsId))
            throw new InvalidOperationException("The selected LLM is not available.");

        _userSelections[userPreferencesId] = settingsId;
    }

    public void Discard(LlmRuntimeSnapshot candidate)
    {
    }
}
