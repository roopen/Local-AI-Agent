using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Tests.TestInfrastructure;

namespace LocalAIAgent.Tests.UseCaseTests;

public class LoadLLMUseCaseTests
{
    private static AIOptions MakeOptions() => new()
    {
        ModelId = "test-model",
        EndpointUrl = "http://localhost:1234/v1/",
    };

    [Fact]
    public async Task LoadLLMUseCase_SendsProviderNeutralChatWarmup()
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("hello");
        LoadLLMUseCase sut = new(new FakeLlmRuntimeManager(MakeOptions(), chat));

        bool result = await sut.LoadLLMUseCaseAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        RecordedCall call = Assert.Single(chat.Calls);
        Assert.False(call.Streaming);
        Assert.Equal("hi", Assert.Single(call.Messages).Text);
        Assert.Equal(1, call.Options?.MaxOutputTokens);
    }

    [Fact]
    public async Task LoadLLMUseCase_ChatWarmupFails_ReturnsFalse()
    {
        FakeChatClient chat = new();
        FakeLlmRuntimeManager runtime = new(MakeOptions(), chat)
        {
            WarmUpException = new LlmConnectionException("Connection failed."),
        };
        LoadLLMUseCase sut = new(runtime);

        bool result = await sut.LoadLLMUseCaseAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Equal(1, runtime.WarmUpCalls);
        Assert.Empty(chat.Calls);
    }
}
