using LocalAIAgent.Tests.Generated;

namespace LocalAIAgent.Tests.IntegrationTests;

public class UserPreferencesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task CreateAndGetUserPreferences_ShouldSucceed()
    {
        // Arrange
        var client = new UserClient("http://localhost", factory.CreateClient());
        var newUser = new UserDto { Username = "testuser" };

        // Act
        // Create User
        var createdUser = await client.UserAsync(newUser);
        Assert.NotNull(createdUser);

        // Save Preferences
        var newPreferences = new UserPreferenceDto { UserId = createdUser.Id, Prompt = "test prompt", Interests = ["coding", "testing"], Dislikes = ["bugs"] };
        await client.SavePreferencesAsync(newPreferences);

        // Get Preferences
        var retrievedPreferences = await client.UserPreferencesAsync(createdUser.Id);

        // Assert
        Assert.NotNull(retrievedPreferences);
        Assert.Equal(newPreferences.Prompt, retrievedPreferences.Prompt);
        Assert.Equal(newPreferences.Interests, retrievedPreferences.Interests);
        Assert.Equal(newPreferences.Dislikes, retrievedPreferences.Dislikes);
    }
}