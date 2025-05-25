using LocalAIAgent.ConsoleApp;
using LocalAIAgent.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

IHost host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddSemanticKernel();
    })
.Build();

Kernel kernel = host.Services.GetRequiredService<Kernel>();

await kernel.StartAIChatInConsole();