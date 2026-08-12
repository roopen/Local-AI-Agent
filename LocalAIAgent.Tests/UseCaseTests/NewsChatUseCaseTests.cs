using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.UseCaseTests;

public class NewsChatUseCaseTests
{
    [Fact]
    public async Task ExpandedNewsUsesClientAndOptionsFromSameRuntimeSnapshot()
    {
        FakeChatClient chat = new();
        chat.EnqueueStreamingText(
            """{"ArticleWasTranslated":true,"Translation":"Hello","TermsAndExplanations":[]}""");
        AIOptions options = new()
        {
            ModelId = "runtime-model",
            EndpointUrl = "http://localhost:1234/v1/",
            Temperature = 0.3m,
            TopP = 0.8m,
        };
        NewsChatUseCase useCase = new(new FakeLlmRuntimeManager(options, chat));

        ExpandedNewsResult result = await useCase.GetExpandedNewsAsync("article");

        Assert.True(result.ArticleWasTranslated);
        Assert.Equal("Hello", result.Translation);
        RecordedCall call = Assert.Single(chat.Calls);
        Assert.True(call.Streaming);
        Assert.Equal("article", call.Messages.Last().Text);
        Assert.Equal(0.3f, call.Options?.Temperature);
        Assert.Equal(0.8f, call.Options?.TopP);
        Assert.Equal(ChatResponseFormat.Json, call.Options?.ResponseFormat);
    }
}
