using LocalAIAgent.API.Controllers;
using System.Net.Http.Json;

namespace LocalAIAgent.Tests.IntegrationTests;

public class UserPreferencesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task CreateAndGetUserPreferences_ShouldSucceed()
    {
        // Arrange
        HttpClient client = factory.CreateClient();
        UserDto newUser = new UserDto { Username = "testuser" };

        // Act
        // Create User
        HttpResponseMessage createUserResponse = await client.PostAsJsonAsync("/api/userpreferences/user", newUser);
        createUserResponse.EnsureSuccessStatusCode();
        UserDto? createdUser = await createUserResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(createdUser);

        // Save Preferences
        UserPreferenceDto newPreferences = new UserPreferenceDto { Prompt = "test prompt", Interests = ["coding", "testing"], Dislikes = ["bugs"] };
        HttpResponseMessage savePreferencesResponse = await client.PostAsJsonAsync($"/api/userpreferences/{createdUser.Id}", newPreferences);
        savePreferencesResponse.EnsureSuccessStatusCode();

        // Get Preferences
        HttpResponseMessage getPreferencesResponse = await client.GetAsync($"/api/userpreferences/{createdUser.Id}");
        getPreferencesResponse.EnsureSuccessStatusCode();
        UserPreferenceDto? retrievedPreferences = await getPreferencesResponse.Content.ReadFromJsonAsync<UserPreferenceDto>();

        // Assert
        Assert.NotNull(retrievedPreferences);
        Assert.Equal(newPreferences.Prompt, retrievedPreferences.Prompt);
        Assert.Equal(newPreferences.Interests, retrievedPreferences.Interests);
        Assert.Equal(newPreferences.Dislikes, retrievedPreferences.Dislikes);
    }
}