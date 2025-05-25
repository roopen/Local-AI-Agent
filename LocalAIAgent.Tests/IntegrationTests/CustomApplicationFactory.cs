using LocalAIAgent.ConsoleApp;
using LocalAIAgent.SemanticKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.Tests.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests", EnvironmentVariableTarget.Process);

            builder.ConfigureServices(services =>
            {
                IKernelBuilder kernelBuilder = DependencyRegistrar.GetSemanticKernelBuilder();

                kernelBuilder.Services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, Mocks.MockEmbeddingGenerator>();

                services.AddSingleton(sp => kernelBuilder.Build());
            });

            return builder.Build();
        }
    }
}
