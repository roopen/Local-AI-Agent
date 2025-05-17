using Local_AI_Agent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;



IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(
        modelId: "gemma-3-27b-it-abliterated", // Adjust based on the model you're using
        apiKey: "", // LM Studio doesn't require an API key
        endpoint: new Uri("http://localhost:1234/v1/")
    );

kernelBuilder.Plugins.AddFromType<TimeService>();

Kernel kernel = kernelBuilder.Build();

OpenAIPromptExecutionSettings settings = new()
{
    ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions
};

IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

ChatHistory chatHistory = [];

StringBuilder fullAssistantContent = new();

while (true)
{
    Console.Write("\nUser: ");
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
}