using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.Extensions;
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