using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.Chat;
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
            IGetNewsUseCase getNewsUseCase = kernel.Services.GetRequiredService<IGetNewsUseCase>();
            ChatService chatService = kernel.Services.GetRequiredService<ChatService>();

            List<NewsItem> newsArticles = await getNewsUseCase.GetAsync(await ChatSetup.ReadUserPreferencesFromFile(chatService));

            DisplayNews(newsArticles);

            //if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is not "IntegrationTests")
            //    await kernel.StartAIChatInConsole();
        }

        private static void DisplayNews(List<NewsItem> newsArticles)
        {
            foreach (NewsItem newsArticle in newsArticles.Take(20))
            {
                Console.WriteLine($"{newsArticle.Content}");
                Console.WriteLine($"Link: {newsArticle.Link}");
                Console.WriteLine($"Published: {newsArticle.PublishDate}");
                Console.WriteLine(new string('-', 80));
            }
        }
    }
}