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
using Microsoft.Extensions.Configuration;

namespace LocalAIAgent.Tests.UnitTests;

public class AiSettingsControllerTests : InMemoryDbTestBase
{
    private readonly AiSettingsSecretProtector _protector = new(new EphemeralDataProtectionProvider());

    private async Task<User> SeedUserAsync(string username, string? token = null, string model = "old-model")
    {
        User user = new()
        {
            Username = username,
            PasswordHash = "hash",
            Fido2Id = [1, 2, 3],
            Preferences = new UserPreferences
            {
                Prompt = "be helpful",
                Interests = ["AI"],
                Dislikes = [],
            },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (token is not null)
        {
            Db.AiSettings.Add(new AiSettings
            {
                ModelId = model,
                EndpointUrl = "http://localhost:1234/v1/",
                ApiKeyCiphertext = _protector.Protect(token),
            });
            await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return user;
    }

    private AiSettingsController CreateController(
        int? userId,
        FakeLlmRuntimeManager? runtime = null,
        IAiSettingsSecretProtector? protector = null,
        string role = AuthRoles.Owner)
    {
        FakeChatClient chat = new();
        chat.EnqueueResponseText("hello");
        runtime ??= new FakeLlmRuntimeManager(new AIOptions
        {
            ModelId = "test-model",
            EndpointUrl = "http://localhost:1234/v1/",
        }, chat) { IsConfigured = false };
        IConfiguration configuration = new ConfigurationBuilder().Build();
        AiSettingsController controller = new(Db, protector ?? _protector, runtime, configuration);
        ClaimsIdentity identity = new(authenticationType: "Test");
        if (userId is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
    }

    private static UpdateAiSettingsRequest MakeRequest(
        string model = "new-model",
        string? apiKey = null,
        bool clearApiKey = false) => new()
    {
        ModelId = model,
        EndpointUrl = "http://localhost:1234/v1/",
        ApiKey = apiKey,
        ClearApiKey = clearApiKey,
        Temperature = 0.5m,
        TopP = 0.42m,
        FrequencyPenalty = 0.1m,
        PresencePenalty = -0.1m,
    };

    [Fact]
    public async Task Get_ReturnsStatusButNeverReturnsToken()
    {
        User user = await SeedUserAsync("alice", "super-secret");
        AiSettingsController controller = CreateController(user.Id);

        ActionResult<AiSettingsResponse> result = await controller.Get(TestContext.Current.CancellationToken);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        AiSettingsResponse response = Assert.IsType<AiSettingsResponse>(ok.Value);
        Assert.True(response.IsConfigured);
        Assert.True(response.HasApiKey);
        Assert.DoesNotContain("secret", System.Text.Json.JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task Get_ForMemberReturnsOnlySafeConfiguredStatus()
    {
        User user = await SeedUserAsync("member", "super-secret");
        AiSettingsController controller = CreateController(user.Id, role: AuthRoles.Member);

        ActionResult<AiSettingsResponse> result = await controller.Get(TestContext.Current.CancellationToken);

        AiSettingsResponse response = Assert.IsType<AiSettingsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(response.IsConfigured);
        Assert.False(response.HasApiKey);
        Assert.Empty(response.ModelId);
        Assert.Empty(response.EndpointUrl);
        Assert.Equal(0, response.Temperature);
        Assert.Equal(0, response.TopP);
        Assert.Equal(0, response.FrequencyPenalty);
        Assert.Equal(0, response.PresencePenalty);
    }

    [Fact]
    public async Task Put_WarmsUpEncryptsPersistsAndActivatesSharedSettings()
    {
        await SeedUserAsync("alice");
        User bob = await SeedUserAsync("bob");
        FakeChatClient chat = new();
        chat.EnqueueResponseText("hello");
        FakeLlmRuntimeManager runtime = new(new AIOptions
        {
            ModelId = "test-model",
            EndpointUrl = "http://localhost:1234/v1/",
        }, chat) { IsConfigured = false };
        AiSettingsController controller = CreateController(bob.Id, runtime);

        ActionResult<AiSettingsResponse> result = await controller.Put(
            MakeRequest(apiKey: "unsloth-token") with { UseResultsForDataset = true },
            TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, runtime.WarmUpCalls);
        Assert.True(runtime.IsConfigured);
        Assert.True(runtime.GetRequiredSnapshot().Options.UseResultsForDataset);
        Assert.Equal("new-model", runtime.GetRequiredSnapshot().Options.ModelId);

        AiSettings saved = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.StartsWith("dp:v1:", saved.ApiKeyCiphertext);
        Assert.DoesNotContain("unsloth-token", saved.ApiKeyCiphertext);
        Assert.Equal("unsloth-token", _protector.Unprotect(saved.ApiKeyCiphertext));
        Assert.Equal(0.42m, saved.TopP);
        Assert.True(saved.UseResultsForDataset);
    }

    [Fact]
    public async Task Put_FailedWarmupReturns422AndLeavesDatabaseAndRuntimeUnchanged()
    {
        User user = await SeedUserAsync("alice", "old-token");
        FakeChatClient chat = new();
        FakeLlmRuntimeManager runtime = new(
            new AIOptions { ModelId = "active-model", EndpointUrl = "http://active/v1/" },
            chat)
        {
            WarmUpException = new LlmConnectionException("The AI service rejected the API token."),
        };
        AiSettingsController controller = CreateController(user.Id, runtime);

        ActionResult<AiSettingsResponse> result = await controller.Put(
            MakeRequest(model: "should-not-save", apiKey: "bad-token"),
            TestContext.Current.CancellationToken);

        UnprocessableEntityObjectResult invalid = Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(invalid.Value);
        Assert.Equal(422, problem.Status);
        Assert.Equal("The AI service rejected the API token.", problem.Detail);
        Assert.Equal("active-model", runtime.GetRequiredSnapshot().Options.ModelId);

        AiSettings stored = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("old-model", stored.ModelId);
        Assert.Equal("old-token", _protector.Unprotect(stored.ApiKeyCiphertext));
    }

    [Fact]
    public async Task Put_NullTokenPreservesExistingToken()
    {
        User user = await SeedUserAsync("alice", "keep-me");
        AiSettingsController controller = CreateController(user.Id);

        await controller.Put(MakeRequest(apiKey: null), TestContext.Current.CancellationToken);

        AiSettings stored = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("keep-me", _protector.Unprotect(stored.ApiKeyCiphertext));
    }

    [Fact]
    public async Task Put_ClearTokenRemovesExistingToken()
    {
        User user = await SeedUserAsync("alice", "remove-me");
        AiSettingsController controller = CreateController(user.Id);

        ActionResult<AiSettingsResponse> result = await controller.Put(
            MakeRequest(apiKey: "ignored", clearApiKey: true),
            TestContext.Current.CancellationToken);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        AiSettingsResponse response = Assert.IsType<AiSettingsResponse>(ok.Value);
        Assert.False(response.HasApiKey);
        AiSettings stored = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Empty(stored.ApiKeyCiphertext);
    }

    [Fact]
    public async Task EndpointsWithoutCurrentUserClaimReturnUnauthorized()
    {
        AiSettingsController controller = CreateController(null);

        ActionResult<AiSettingsResponse> get = await controller.Get(TestContext.Current.CancellationToken);
        ActionResult<AiSettingsResponse> put = await controller.Put(
            MakeRequest(),
            TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(get.Result);
        Assert.IsType<UnauthorizedResult>(put.Result);
    }

    [Fact]
    public async Task LostDataProtectionKeyRequiresTokenReentry()
    {
        User user = await SeedUserAsync("alice", "saved-token");
        AiSettingsSecretProtector replacementProtector = new(new EphemeralDataProtectionProvider());
        AiSettingsController controller = CreateController(user.Id, protector: replacementProtector);

        ActionResult<AiSettingsResponse> getResult = await controller.Get(TestContext.Current.CancellationToken);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(getResult.Result);
        AiSettingsResponse response = Assert.IsType<AiSettingsResponse>(ok.Value);
        Assert.False(response.IsConfigured);
        Assert.False(response.HasApiKey);

        ActionResult<AiSettingsResponse> preserveResult = await controller.Put(
            MakeRequest(apiKey: null),
            TestContext.Current.CancellationToken);
        UnprocessableEntityObjectResult invalid = Assert.IsType<UnprocessableEntityObjectResult>(preserveResult.Result);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(invalid.Value);
        Assert.Contains("Enter it again", problem.Detail);
    }
}
