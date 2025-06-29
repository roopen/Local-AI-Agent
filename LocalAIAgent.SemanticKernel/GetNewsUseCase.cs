using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalAIAgent.SemanticKernel
{
    public interface IGetNewsUseCase
    {
        Task<List<NewsItem>> GetAsync();
    }

    public class GetNewsUseCase : IGetNewsUseCase
    {
        private readonly INewsService _newsService;
        private readonly ChatService _chatService;

        public GetNewsUseCase(INewsService newsService, Kernel kernel)
        {
            _newsService = newsService;
            _chatService = new(
                kernel.Services.GetService<IChatCompletionService>()!,
                kernel,
                kernel.Services.GetService<AIOptions>()!,
                kernel.Services.GetService<ChatContext>()!
            );

            kernel.LoadUserPromptIntoChatContext(_chatService, GetUserPreferencesPrompt()).GetAwaiter().GetResult();
        }

        public static string GetUserPreferencesPrompt()
        {
            if (!File.Exists("UserPrompt.txt"))
            {
                Console.WriteLine("UserPrompt.txt not found.");
                return string.Empty;
            }
            else
            {
                Console.WriteLine("UserPrompt.txt found.");
                return File.ReadAllText("UserPrompt.txt");
            }
        }

        public async Task<List<NewsItem>> GetAsync()
        {
            List<NewsItem> allNews = await _newsService.GetNewsAsync();

            List<NewsItem> filteredNews = await FilteredNews(allNews);

            return filteredNews;
        }

        private async Task<List<NewsItem>> FilteredNews(List<NewsItem> allNews)
        {
            List<NewsItem> filteredNews = [];

            foreach (NewsItem newsArticle in allNews)
            {
                if (string.IsNullOrWhiteSpace(newsArticle.Content))
                    continue;

                await foreach (StreamingChatMessageContent content in _chatService.EvaluateArticles(newsArticle.Content))
                {
                    if (!bool.TryParse(content.Content, out bool shouldInclude)) continue;

                    if (shouldInclude)
                    {
                        Console.WriteLine("Included article: \n" + newsArticle.Content);
                        filteredNews.Add(newsArticle);
                    }
                }
            }

            return filteredNews;
        }
    }

    public record NewsRequest
    {
        public string Prompt { get; init; } = string.Empty;
        public List<string> Likes { get; init; } = [];
        public List<string> Dislikes { get; init; } = [];

        public bool IsEmpty() => string.IsNullOrWhiteSpace(Prompt) && Likes.Count == 0 && Dislikes.Count == 0;
    }
}
