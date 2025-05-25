using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.Tests.IntegrationTests
{
    public class NewsServiceTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task LoadAllNews_ShouldReturnNewsItems()
        {
            // Arrange
            using IServiceScope scope = factory.Services.CreateScope();
            Kernel semanticKernel = scope.ServiceProvider.GetRequiredService<Kernel>();
            NewsService newsService = semanticKernel.Services.GetRequiredService<NewsService>();
            ChatContext chatContext = semanticKernel.Services.GetRequiredService<ChatContext>();
            chatContext.UserDislikes = ["1", "2", "3", "4", "5"];

            // Act
            int newsCount = await newsService.LoadAllNews();
            List<string> newsItems = await newsService.GetNewsAsync();

            // Assert
            Assert.True(newsCount > 0);
            Assert.NotEmpty(newsItems);
        }
    }
}