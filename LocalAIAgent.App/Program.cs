using LocalAIAgent.App;
using LocalAIAgent.App.Chat;
using LocalAIAgent.App.Extensions;
using LocalAIAgent.App.News;
using LocalAIAgent.App.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NodaTime;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.Services.AddNewsClients();
kernelBuilder.Services.AddSingleton<ChatService>();
kernelBuilder.Services.AddSingleton<IClock>(SystemClock.Instance);
IConfiguration configuration = kernelBuilder.Services.AddConfigurations();

AIOptions? aiOptions = configuration.GetSection("AIOptions").Get<AIOptions>() ?? throw new Exception("AIOptions not found in configuration.");

kernelBuilder.Plugins.AddFromType<TimeService>();
kernelBuilder.Plugins.AddFromType<NewsService>();

kernelBuilder
    .AddOpenAIChatCompletion(
        modelId: aiOptions.ModelId,
        apiKey: aiOptions.ApiKey,
        endpoint: new Uri(aiOptions.EndpointUrl)
    );

Kernel kernel = kernelBuilder.Build();

await StartAiChat(kernel, aiOptions);

static async Task StartAiChat(Kernel kernel, AIOptions options)
{
    ChatService chatService = new(
        kernel.Services.GetService<IChatCompletionService>()!,
        kernel,
        options
    );
    await chatService.StartChat();
}