using System.Security.Claims;
using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LocalAIAgent.Tests.UnitTests;

public sealed class AdminControllerTests : InMemoryDbTestBase
{
    private async Task<User> SeedUserAsync(string name, UserRole role)
    {
        User user = new()
        {
            Username = name,
            PasswordHash = "hash",
            Fido2Id = [1, (byte)(role == UserRole.Owner ? 2 : 3)],
            Role = role,
            Preferences = new UserPreferences { Prompt = "p", Interests = [], Dislikes = [] },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private AdminController CreateController(int ownerId)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PUBLIC_ORIGIN"] = "https://news.example.com",
            })
            .Build();
        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, ownerId.ToString()),
                new Claim(ClaimTypes.Role, AuthRoles.Owner),
            ],
            "Test");
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(identity),
        };
        return new AdminController(Db, configuration, TimeProvider.System)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    [Fact]
    public async Task CreateInvitation_ReturnsRawTokenOnceAndStoresOnlyHash()
    {
        User owner = await SeedUserAsync("owner", UserRole.Owner);
        AdminController controller = CreateController(owner.Id);

        ActionResult<CreatedInvitationDto> result = await controller.CreateInvitation(TestContext.Current.CancellationToken);

        CreatedInvitationDto created = Assert.IsType<CreatedInvitationDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.StartsWith("https://news.example.com/register#invite=", created.InviteUrl);
        string token = new Uri(created.InviteUrl).Fragment["#invite=".Length..];
        Invitation stored = await Db.Invitations.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(InvitationTokens.Hash(token), stored.TokenHash);
        Assert.InRange(stored.ExpiresAt - stored.CreatedAt, TimeSpan.FromDays(6.99), TimeSpan.FromDays(7.01));

        ActionResult<IReadOnlyList<InvitationDto>> listResult = await controller.ListInvitations(TestContext.Current.CancellationToken);
        IReadOnlyList<InvitationDto> listed = Assert.IsAssignableFrom<IReadOnlyList<InvitationDto>>(
            Assert.IsType<OkObjectResult>(listResult.Result).Value);
        string serialized = System.Text.Json.JsonSerializer.Serialize(listed);
        Assert.DoesNotContain(token, serialized);
        Assert.DoesNotContain(stored.TokenHash, serialized);
    }

    [Fact]
    public async Task RevokeInvitation_MarksUnusedInviteAndRejectsRedeemedInvite()
    {
        User owner = await SeedUserAsync("owner", UserRole.Owner);
        AdminController controller = CreateController(owner.Id);
        CreatedInvitationDto created = Assert.IsType<CreatedInvitationDto>(
            Assert.IsType<OkObjectResult>((await controller.CreateInvitation(TestContext.Current.CancellationToken)).Result).Value);

        Assert.IsType<NoContentResult>(await controller.RevokeInvitation(created.Id, TestContext.Current.CancellationToken));
        Assert.NotNull((await Db.Invitations.SingleAsync(TestContext.Current.CancellationToken)).RevokedAt);

        Invitation redeemed = new()
        {
            TokenHash = InvitationTokens.Hash(InvitationTokens.Create()),
            CreatedByUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RedeemedAt = DateTimeOffset.UtcNow,
            RedeemedByUserId = owner.Id,
        };
        Db.Invitations.Add(redeemed);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.IsType<ConflictObjectResult>(await controller.RevokeInvitation(redeemed.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserState_DisablesMemberButNeverOwner()
    {
        User owner = await SeedUserAsync("owner", UserRole.Owner);
        User member = await SeedUserAsync("member", UserRole.Member);
        AdminController controller = CreateController(owner.Id);

        Assert.IsType<NoContentResult>(await controller.SetUserState(
            member.Id,
            new UpdateManagedUserDto(true),
            TestContext.Current.CancellationToken));
        Assert.True((await Db.Users.FindAsync([member.Id], TestContext.Current.CancellationToken))!.IsDisabled);

        Assert.IsType<BadRequestObjectResult>(await controller.SetUserState(
            owner.Id,
            new UpdateManagedUserDto(true),
            TestContext.Current.CancellationToken));
        Assert.False((await Db.Users.FindAsync([owner.Id], TestContext.Current.CancellationToken))!.IsDisabled);
    }
}
