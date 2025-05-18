using LocalAIAgent.App.Time;
using Moq;
using NodaTime;
using System.Globalization;

namespace LocalAIAgent.Tests
{
    public class TimeServiceTests
    {
        private readonly DateTime testDate = new(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void GetCurrentTimeInUtc_Returns_ValidTime()
        {
            // Arrange
            Mock<IClock> mockClock = new();
            mockClock.Setup(clock => clock.GetCurrentInstant()).Returns(Instant.FromDateTimeUtc(testDate));
            TimeService timeService = new(mockClock.Object);

            // Act
            string currentTime = timeService.GetCurrentTimeInUtc();

            // Assert
            Assert.NotNull(currentTime);
            Assert.IsType<string>(currentTime);
            Assert.Equal(currentTime, testDate.ToString("F", new CultureInfo("en-US")));
        }
    }
}
