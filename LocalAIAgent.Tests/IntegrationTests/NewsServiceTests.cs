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
            INewsService newsService = semanticKernel.Services.GetRequiredService<INewsService>();

            // Act
            List<NewsItem> newsItems = await newsService.GetNewsAsync();

            // Assert
            Assert.NotEmpty(newsItems);
        }
    }
}