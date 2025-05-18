using LocalAIAgent.App.Time;

namespace LocalAIAgent.Tests
{
    public class TimeServiceTests
    {
        [Fact]
        public void TestGetCurrentTime()
        {
            // Arrange
            TimeService timeService = new();
            // Act
            string currentTime = timeService.GetCurrentTimeInUtc();
            // Assert
            Assert.NotNull(currentTime);
            Assert.IsType<string>(currentTime);
            Assert.True(DateTime.TryParse(currentTime, out _), "The current time should be in a valid DateTime format.");
        }
    }
}
