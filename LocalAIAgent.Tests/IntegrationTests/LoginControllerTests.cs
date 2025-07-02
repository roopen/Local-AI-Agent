using LocalAIAgent.Tests.Generated;
using System.Net;

namespace LocalAIAgent.Tests.IntegrationTests;

public class LoginControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task RegisterAndLogin_ShouldSucceed()
    {
        // Arrange
        UserClient client = new("http://localhost", factory.CreateClient());
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
        UserClient client = new("http://localhost", factory.CreateClient());
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
}