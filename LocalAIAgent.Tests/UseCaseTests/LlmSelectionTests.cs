using LocalAIAgent.Application.Chat;

namespace LocalAIAgent.Tests.UseCaseTests;

public sealed class LlmSelectionTests
{
    [Fact]
    public void ChangingUserSelectionChangesTheNextResolvedRuntime()
    {
        using LlmRuntimeManager runtime = new(new AIApplicationOptions());
        LlmRuntimeSnapshot first = runtime.CreateCandidate(Settings("first-model"));
        LlmRuntimeSnapshot second = runtime.CreateCandidate(Settings("second-model"));

        runtime.Activate(10, first);
        runtime.Activate(20, second);
        runtime.SetUserSelection(userPreferencesId: 7, settingsId: 10);

        Assert.Equal(
            "first-model",
            runtime.GetRequiredSnapshot(userPreferencesId: 7).Options.ModelId);

        runtime.SetUserSelection(userPreferencesId: 7, settingsId: 20);

        Assert.Equal(
            "second-model",
            runtime.GetRequiredSnapshot(userPreferencesId: 7).Options.ModelId);
    }

    [Fact]
    public void UsersCanResolveDifferentRuntimesConcurrently()
    {
        using LlmRuntimeManager runtime = new(new AIApplicationOptions());
        runtime.Activate(10, runtime.CreateCandidate(Settings("first-model")));
        runtime.Activate(20, runtime.CreateCandidate(Settings("second-model")));
        runtime.SetUserSelection(userPreferencesId: 1, settingsId: 10);
        runtime.SetUserSelection(userPreferencesId: 2, settingsId: 20);

        Assert.Equal("first-model", runtime.GetRequiredSnapshot(1).Options.ModelId);
        Assert.Equal("second-model", runtime.GetRequiredSnapshot(2).Options.ModelId);
    }

    private static LlmConnectionSettings Settings(string modelId) => new(
        modelId,
        "http://localhost:1234/v1/",
        string.Empty,
        0.2m,
        1m,
        0m,
        0m);
}
