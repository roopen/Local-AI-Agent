using LocalAIAgent.App.News;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.Tests.UnitTests
{
    public class NewsItemTests
    {
        [Fact]
        public void NewsItem_IsCreated_Successfully()
        {
            // Arrange
            SyndicationItem syndicationItem = new()
            {
                Title = new TextSyndicationContent("Test Title"),
                Summary = new TextSyndicationContent("Test Summary"),
                PublishDate = new DateTimeOffset(),
            };
            syndicationItem.Links.Add(new SyndicationLink(new Uri("http://example.com")));

            // Act
            NewsItem newsItem = new(syndicationItem);
            string newsItemJson = newsItem.ToString();

            // Assert
            Assert.NotNull(newsItem);
            Assert.Contains("Test Title", newsItemJson);
            Assert.Contains("Test Summary", newsItemJson);
            Assert.Contains("http://example.com", newsItemJson);
            string expectedDateAsJson = System.Text.Json.JsonSerializer.Serialize(new DateTimeOffset());
            Assert.Contains(expectedDateAsJson, newsItemJson);
        }

        [Fact]
        public void NewsItem_IsCreated_WithNulls()
        {
            // Arrange
            SyndicationItem syndicationItem = new()
            {
                Title = null,
                Summary = null,
                PublishDate = new DateTimeOffset(),
            };

            // Act
            NewsItem newsItem = new(syndicationItem);
            string newsItemJson = newsItem.ToString();

            // Assert
            Assert.NotNull(newsItem);
            string expectedDateAsJson = System.Text.Json.JsonSerializer.Serialize(new DateTimeOffset());
            Assert.Contains(expectedDateAsJson, newsItemJson);
        }
    }
}
