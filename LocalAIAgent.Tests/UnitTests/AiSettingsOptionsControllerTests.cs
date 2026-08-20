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
    public async Task OwnerCanCreateModelsOnSharedAndSeparateHostsAndSelectOne()
    {
        User user = await SeedUserAsync(UserRole.Owner);
        FakeChatClient chat = new();
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        FakeLlmRuntimeManager runtime = CreateRuntime(chat);
        AiSettingsOptionsController controller = CreateController(user.Id, UserRole.Owner, runtime);

        AiSettingsOptionResponse first = GetResponse(await controller.CreateOption(
            Request("Local small", "first-model", apiKey: "shared-token"),
            TestContext.Current.CancellationToken));
        AiSettingsOptionResponse second = GetResponse(await controller.CreateOption(
            Request(
                "Local large",
                "second-model",
                endpointUrl: null,
                connectionSourceSettingsId: first.Id),
            TestContext.Current.CancellationToken));
        AiSettingsOptionResponse remote = GetResponse(await controller.CreateOption(
            Request(
                "Remote",
                "remote-model",
                apiKey: "remote-token",
                endpointUrl: "https://llm.example/v1/"),
            TestContext.Current.CancellationToken));

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, remote.Id);
        Assert.Equal(3, runtime.WarmUpCalls);
        Assert.True(runtime.IsConfiguredFor(first.Id));
        Assert.True(runtime.IsConfiguredFor(second.Id));
        Assert.True(runtime.IsConfiguredFor(remote.Id));
        Assert.Equal(3, await Db.AiSettings.CountAsync(TestContext.Current.CancellationToken));

        AiSettings sharedModel = await Db.AiSettings.SingleAsync(
            option => option.Id == second.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(first.Id, first.HostId);
        Assert.Equal(first.Id, second.HostId);
        Assert.Equal(first.Id, sharedModel.HostId);
        Assert.Equal("http://localhost:1234/v1/", sharedModel.EndpointUrl);
        Assert.True(_protector.TryUnprotect(sharedModel.ApiKeyCiphertext, out string sharedApiKey));
        Assert.Equal("shared-token", sharedApiKey);

        AiSettings remoteModel = await Db.AiSettings.SingleAsync(
            option => option.Id == remote.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(remote.Id, remote.HostId);
        Assert.Equal(remote.Id, remoteModel.HostId);
        Assert.Equal("https://llm.example/v1/", remoteModel.EndpointUrl);
        Assert.True(_protector.TryUnprotect(remoteModel.ApiKeyCiphertext, out string remoteApiKey));
        Assert.Equal("remote-token", remoteApiKey);

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
    public async Task EditingSharedHostConnectionRetestsAndUpdatesEveryModelOnHost()
    {
        User user = await SeedUserAsync(UserRole.Owner);
        FakeChatClient chat = new();
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        chat.EnqueueResponseText("ok");
        FakeLlmRuntimeManager runtime = CreateRuntime(chat);
        AiSettingsOptionsController controller = CreateController(user.Id, UserRole.Owner, runtime);

        AiSettingsOptionResponse first = GetResponse(await controller.CreateOption(
            Request("Small", "small-model", apiKey: "old-token"),
            TestContext.Current.CancellationToken));
        AiSettingsOptionResponse second = GetResponse(await controller.CreateOption(
            Request(
                "Large",
                "large-model",
                endpointUrl: null,
                connectionSourceSettingsId: first.Id),
            TestContext.Current.CancellationToken));

        GetResponse(await controller.UpdateOption(
            first.Id,
            Request(
                "Small",
                "small-model",
                apiKey: "new-token",
                endpointUrl: "https://replacement.example/v1/"),
            TestContext.Current.CancellationToken));

        Assert.Equal(4, runtime.WarmUpCalls);
        List<AiSettings> savedModels = await Db.AiSettings
            .OrderBy(option => option.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(savedModels, option =>
        {
            Assert.Equal(first.Id, option.HostId);
            Assert.Equal("https://replacement.example/v1/", option.EndpointUrl);
            Assert.True(_protector.TryUnprotect(option.ApiKeyCiphertext, out string apiKey));
            Assert.Equal("new-token", apiKey);
        });
        Assert.Equal(
            "https://replacement.example/v1/",
            runtime.GetRequiredSnapshot().Options.EndpointUrl);
        runtime.SetUserSelection(user.Preferences!.Id, second.Id);
        Assert.Equal(
            "https://replacement.example/v1/",
            runtime.GetRequiredSnapshot(user.Preferences.Id).Options.EndpointUrl);
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
        string? apiKey = null,
        string? endpointUrl = "http://localhost:1234/v1/",
        int? connectionSourceSettingsId = null) => new()
    {
        Name = name,
        ModelId = modelId,
        EndpointUrl = endpointUrl,
        ConnectionSourceSettingsId = connectionSourceSettingsId,
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
