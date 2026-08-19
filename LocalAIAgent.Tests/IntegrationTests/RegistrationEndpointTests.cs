using System.Net;
using System.Net.Http.Json;
using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Tests.IntegrationTests;

public sealed class RegistrationEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    [Fact]
    public async Task EmptyInstall_AllowsOwnerRegistrationThenRequiresInvite()
    {
        RegistrationStatusDto? emptyStatus = await _httpClient.GetFromJsonAsync<RegistrationStatusDto>(
            "/api/auth/registration-status",
            TestContext.Current.CancellationToken);

        Assert.NotNull(emptyStatus);
        Assert.Equal("OwnerBootstrap", emptyStatus.Mode);

        HttpResponseMessage ownerOptions = await _httpClient.PostAsJsonAsync(
            "/api/auth/register/options",
            new RegistrationOptionsRequest("first-owner", null),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownerOptions.StatusCode);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            UserContext context = scope.ServiceProvider.GetRequiredService<UserContext>();
            context.Users.Add(new User
            {
                Fido2Id = [1],
                Username = "existing-owner",
                Role = UserRole.Owner,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        RegistrationStatusDto? existingStatus = await _httpClient.GetFromJsonAsync<RegistrationStatusDto>(
            "/api/auth/registration-status",
            TestContext.Current.CancellationToken);

        Assert.NotNull(existingStatus);
        Assert.Equal("InviteRequired", existingStatus.Mode);

        HttpResponseMessage memberOptions = await _httpClient.PostAsJsonAsync(
            "/api/auth/register/options",
            new RegistrationOptionsRequest("uninvited-member", null),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, memberOptions.StatusCode);
    }
}
