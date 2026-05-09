using LocalAIAgent.Application.News;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Tests.IntegrationTests
{
    public class NewsServiceTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
    {
        [Fact]
        public async Task LoadAllNews_ShouldReturnNewsItems()
        {
            // Arrange
            using IServiceScope scope = factory.Services.CreateScope();
            INewsService newsService = scope.ServiceProvider.GetRequiredService<INewsService>();

            // Act
            List<NewsItem> newsItems = await newsService.GetNewsAsync();

            // Assert
            Assert.NotEmpty(newsItems);
        }
    }
}
