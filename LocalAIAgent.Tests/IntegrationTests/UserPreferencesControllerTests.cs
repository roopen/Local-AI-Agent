using LocalAIAgent.Tests.Generated;

namespace LocalAIAgent.Tests.IntegrationTests;

public class UserPreferencesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task RegisterAndGetUserPreferences_ShouldSucceed()
    {
        // Arrange
        UserClient client = new("https://localhost", factory.CreateClient());
        UserRegistrationDto newUser = new() { Username = "testuser", Password = "password" };
        UserDto createdUser = await client.RegisterAsync(newUser);
        UserLoginDto loginUser = new() { Username = "testuser", Password = "password" };
        UserDto loggedInUser = await client.LoginAsync(loginUser);

        // Act
        UserPreferenceDto newPreferences = new()
        {
            UserId = loggedInUser.Id,
            Prompt = "test prompt",
            Interests = ["coding", "testing"],
            Dislikes = ["bugs"]
        };
        await client.SavePreferencesAsync(newPreferences);

        UserPreferenceDto retrievedPreferences = await client.UserPreferencesAsync(loggedInUser.Id);

        // Assert
        Assert.NotNull(retrievedPreferences);
        Assert.Equal(newPreferences.Prompt, retrievedPreferences.Prompt);
        Assert.Equal(newPreferences.Interests, retrievedPreferences.Interests);
        Assert.Equal(newPreferences.Dislikes, retrievedPreferences.Dislikes);
    }
}