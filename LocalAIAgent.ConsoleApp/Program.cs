using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.News;
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
            IGetNewsUseCase getNewsUseCase = host.Services.GetRequiredService<IGetNewsUseCase>();

            List<NewsItem> newsArticles = await getNewsUseCase.GetAsync();

            DisplayNews(newsArticles);

            //if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is not "IntegrationTests")
            //    await kernel.StartAIChatInConsole();
        }

        private static void DisplayNews(List<NewsItem> newsArticles)
        {
            foreach (NewsItem newsArticle in newsArticles.Take(10))
            {
                Console.WriteLine($"{newsArticle.Content}");
                Console.WriteLine($"Link: {newsArticle.Link}");
                Console.WriteLine($"Published: {newsArticle.PublishDate}");
                Console.WriteLine(new string('-', 80));
            }
        }
    }
}