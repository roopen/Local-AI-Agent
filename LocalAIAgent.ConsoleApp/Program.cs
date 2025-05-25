using LocalAIAgent.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.ConsoleApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSemanticKernel();
                })
            .Build();

            Kernel kernel = host.Services.GetRequiredService<Kernel>();

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is not "IntegrationTests")
                await kernel.StartAIChatInConsole();
        }
    }
}