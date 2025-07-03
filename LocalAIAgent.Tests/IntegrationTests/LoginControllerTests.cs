using LocalAIAgent.Tests.Generated;
using System.Net;

namespace LocalAIAgent.Tests.IntegrationTests;

public class LoginControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task RegisterAndLogin_ShouldSucceed()
    {
        // Arrange
        UserClient client = new("https://localhost", factory.CreateClient());
        UserRegistrationDto registration = new() { Username = "testuser", Password = "password" };

        // Act
        // Register User
        UserDto registeredUser = await client.RegisterAsync(registration);
        Assert.NotNull(registeredUser);

        // Login User
        UserLoginDto login = new() { Username = "testuser", Password = "password" };
        UserDto loggedInUser = await client.LoginAsync(login);

        // Assert
        Assert.NotNull(loggedInUser);
        Assert.Equal(registeredUser.Id, loggedInUser.Id);
        Assert.Equal(registeredUser.Username, loggedInUser.Username);
    }

    [Fact]
    public async Task RegisterAndLoginWithWrongPassword_ShouldFail()
    {
        // Arrange
        UserClient client = new("https://localhost", factory.CreateClient());
        UserRegistrationDto registration = new() { Username = "testuser2", Password = "password" };

        // Act
        // Register User
        UserDto registeredUser = await client.RegisterAsync(registration);
        Assert.NotNull(registeredUser);

        // Login User
        UserLoginDto login = new() { Username = "testuser", Password = "wrongpassword" };
        ApiException loginResult = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync(login));

        // Assert
        Assert.NotNull(loginResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, loginResult.StatusCode);
    }

    [Fact]
    public async Task Logout_ShouldSucceed()
    {
        // Arrange
        UserClient client = new("https://localhost", factory.CreateClient());
        UserRegistrationDto registration = new() { Username = "testuser3", Password = "password" };
        UserDto registeredUser = await client.RegisterAsync(registration);
        UserLoginDto login = new() { Username = "testuser3", Password = "password" };
        UserDto loggedInUser = await client.LoginAsync(login);

        // Act
        await client.LogoutAsync();
        ApiException logoutResult = await Assert.ThrowsAsync<ApiException>(client.CurrentAsync);

        // Assert
        Assert.NotNull(logoutResult);
        Assert.Equal((int)HttpStatusCode.Unauthorized, logoutResult.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturnAuthenticatedUser()
    {
        // Arrange
        UserClient client = new("https://localhost", factory.CreateClient());
        UserRegistrationDto registration = new() { Username = "testuser4", Password = "password" };
        UserDto registeredUser = await client.RegisterAsync(registration);
        UserLoginDto login = new() { Username = "testuser4", Password = "password" };
        UserDto loggedInUser = await client.LoginAsync(login);

        // Act
        UserDto currentUser = await client.CurrentAsync();

        // Assert
        Assert.NotNull(currentUser);
        Assert.Equal(loggedInUser.Id, currentUser.Id);
        Assert.Equal(loggedInUser.Username, currentUser.Username);
    }
}