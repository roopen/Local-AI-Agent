using Local_AI_Agent.Chat;
using Local_AI_Agent.News;
using Local_AI_Agent.Time;
using LocalAIAgent.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.Services.AddYleNewsClient();
kernelBuilder.Services.AddFoxNewsClient();
kernelBuilder.Services.AddSingleton<ChatService>();

IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

kernelBuilder.Services.Configure<AIOptions>(configuration.GetSection("AIOptions"));
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