using Local_AI_Agent;
using Local_AI_Agent.News;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(
        modelId: "gemma-3-27b-it-abliterated",
        apiKey: string.Empty,
        endpoint: new Uri("http://localhost:1234/v1/")
    );

kernelBuilder.Services.AddYleNewsClient();

kernelBuilder.Plugins.AddFromType<TimeService>();
kernelBuilder.Plugins.AddFromType<NewsService>();

Kernel kernel = kernelBuilder.Build();

IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

ChatHistory chatHistory = [];

StringBuilder fullAssistantContent = new();

while (true)
{
    Console.Write("User: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) { break; }

    chatHistory.AddUserMessage(input);

    Console.WriteLine("Assistant: ");

    await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
        chatHistory,
        new OpenAIPromptExecutionSettings { ReasoningEffort = "high", FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() },
        kernel)
        .ConfigureAwait(false))
    {
        Console.Write(content.Content);
        fullAssistantContent.Append(content.Content);
    }

    chatHistory.AddAssistantMessage(fullAssistantContent.ToString());
    Console.WriteLine();
}