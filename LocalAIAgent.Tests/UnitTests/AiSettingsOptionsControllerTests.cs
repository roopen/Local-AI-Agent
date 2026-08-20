using System.Security.Claims;
using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.Tests.UnitTests;

public sealed class AiSettingsOptionsControllerTests : InMemoryDbTestBase
{
    private readonly AiSettingsSecretProtector _protector =
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public async Task OwnerCanCreateMultipleTestedOptionsAndSelectOne()
    {
        User user = await SeedUserAsync(UserRole.Owner);
        FakeChatClient chat = new();
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        FakeLlmRuntimeManager runtime = CreateRuntime(chat);
        AiSettingsOptionsController controller = CreateController(user.Id, UserRole.Owner, runtime);

        AiSettingsOptionResponse first = GetResponse(await controller.CreateOption(
            Request("Local", "first-model"),
            TestContext.Current.CancellationToken));
        AiSettingsOptionResponse second = GetResponse(await controller.CreateOption(
            Request("Remote", "second-model"),
            TestContext.Current.CancellationToken));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, runtime.WarmUpCalls);
        Assert.True(runtime.IsConfiguredFor(first.Id));
        Assert.True(runtime.IsConfiguredFor(second.Id));
        Assert.Equal(2, await Db.AiSettings.CountAsync(TestContext.Current.CancellationToken));

        IActionResult selectionResult = await controller.SelectOption(
            new SelectAiSettingsRequest { SettingsId = second.Id },
            TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(selectionResult);
        UserPreferences preferences = await Db.UserPreferences
            .SingleAsync(item => item.UserId == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(second.Id, preferences.SelectedAiSettingsId);
        Assert.Equal(
            "second-model",
            runtime.GetRequiredSnapshot(preferences.Id).Options.ModelId);
    }

    [Fact]
    public async Task MemberCatalogExposesChoicesWithoutConnectionSecrets()
    {
        User owner = await SeedUserAsync(UserRole.Owner);
        User member = await SeedUserAsync(UserRole.Member);
        FakeChatClient chat = new();
        chat.EnqueueResponseText("ok");
        FakeLlmRuntimeManager runtime = CreateRuntime(chat);
        AiSettingsOptionsController ownerController =
            CreateController(owner.Id, UserRole.Owner, runtime);
        AiSettingsOptionResponse saved = GetResponse(await ownerController.CreateOption(
            Request("Private remote", "remote-model", apiKey: "secret"),
            TestContext.Current.CancellationToken));

        AiSettingsOptionsController memberController =
            CreateController(member.Id, UserRole.Member, runtime);
        ActionResult<AiSettingsCatalogResponse> result = await memberController.GetOptions(
            TestContext.Current.CancellationToken);

        AiSettingsCatalogResponse catalog = Assert.IsType<AiSettingsCatalogResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        AiSettingsOptionResponse option = Assert.Single(catalog.Options);
        Assert.Equal(saved.Id, option.Id);
        Assert.Equal("Private remote", option.Name);
        Assert.Equal("remote-model", option.ModelId);
        Assert.Empty(option.EndpointUrl);
        Assert.False(option.HasApiKey);
        Assert.False(catalog.IsOwner);
    }

    private async Task<User> SeedUserAsync(UserRole role)
    {
        User user = new()
        {
            Username = Guid.NewGuid().ToString("N"),
            PasswordHash = "hash",
            Fido2Id = Guid.NewGuid().ToByteArray(),
            Role = role,
            Preferences = new UserPreferences
            {
                Prompt = "prompt",
                Interests = ["AI"],
                Dislikes = [],
            },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static FakeLlmRuntimeManager CreateRuntime(FakeChatClient chat) => new(
        new AIOptions
        {
            ModelId = "unused",
            EndpointUrl = "http://localhost:1234/v1/",
        },
        chat)
    {
        IsConfigured = false,
    };

    private AiSettingsOptionsController CreateController(
        int userId,
        UserRole role,
        FakeLlmRuntimeManager runtime)
    {
        AiSettingsOptionsController controller = new(Db, _protector, runtime);
        ClaimsIdentity identity = new(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, role == UserRole.Owner
            ? AuthRoles.Owner
            : AuthRoles.Member));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };
        return controller;
    }

    private static SaveAiSettingsOptionRequest Request(
        string name,
        string modelId,
        string? apiKey = null) => new()
    {
        Name = name,
        ModelId = modelId,
        EndpointUrl = "http://localhost:1234/v1/",
        ApiKey = apiKey,
        Temperature = 0.2m,
        TopP = 1m,
        FrequencyPenalty = 0m,
        PresencePenalty = 0m,
    };

    private static AiSettingsOptionResponse GetResponse(
        ActionResult<AiSettingsOptionResponse> result) =>
        Assert.IsType<AiSettingsOptionResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
}
