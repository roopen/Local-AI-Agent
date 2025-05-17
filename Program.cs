using Local_AI_Agent;
using Local_AI_Agent.News;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(
        modelId: "gemma-3-27b-it-abliterated",
        apiKey: string.Empty,
        endpoint: new Uri("http://localhost:1234/v1/")
    );

kernelBuilder.Services.AddYleNewsClient();
kernelBuilder.Services.AddFoxNewsClient();
kernelBuilder.Services.AddSingleton<ChatService>();

kernelBuilder.Plugins.AddFromType<TimeService>();
kernelBuilder.Plugins.AddFromType<NewsService>();

Kernel kernel = kernelBuilder.Build();

await StartAiChat(kernel);

static async Task StartAiChat(Kernel kernel)
{
    ChatService chatService = new(
        kernel.Services.GetService<IChatCompletionService>()!,
        kernel
    );
    await chatService.StartChat();
}