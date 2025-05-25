using LocalAIAgent.SemanticKernel.Chat;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.Tests.UnitTests
{
    public class ChatContextTests
    {
        [Fact]
        public void IsArticleRelevant_ShouldReturnTrue_WhenArticleDoesNotContainDislikedWords()
        {
            // Arrange
            ChatContext chatContext = new ChatContext();
            chatContext.UserDislikes.Add("sports");
            chatContext.UserDislikes.Add("politics");
            SyndicationItem syndicationItem = new SyndicationItem
            {
                Title = new TextSyndicationContent("Technology News"),
                Summary = new TextSyndicationContent("Latest updates in technology.")
            };
            // Act
            bool result = chatContext.IsArticleRelevant(syndicationItem);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsArticleRelevant_ShouldReturnFalse_WhenArticleContainsDislikedWords()
        {
            // Arrange
            ChatContext chatContext = new ChatContext();
            chatContext.UserDislikes.Add("sports");
            chatContext.UserDislikes.Add("politics");
            SyndicationItem syndicationItem = new SyndicationItem
            {
                Title = new TextSyndicationContent("Sports Update"),
                Summary = new TextSyndicationContent("Latest updates in sports.")
            };
            // Act
            bool result = chatContext.IsArticleRelevant(syndicationItem);
            // Assert
            Assert.False(result);
        }
    }
}
