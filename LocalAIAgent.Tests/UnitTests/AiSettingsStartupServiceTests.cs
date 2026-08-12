using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Application.Chat;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalAIAgent.Tests.UnitTests;

public class AiSettingsStartupServiceTests : InMemoryDbTestBase
{
    private readonly AiSettingsSecretProtector _protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public async Task NoSettingsSkipsWarmupAndLeavesRuntimeUnconfigured()
    {
        (AiSettingsStartupService service, FakeLlmRuntimeManager runtime, FakeChatClient chat) = CreateService();

        bool result = await service.ActivateFirstAndWarmUpAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(runtime.IsConfigured);
        Assert.Equal(0, runtime.WarmUpCalls);
        Assert.Empty(chat.Calls);
    }

    [Fact]
    public async Task StartupDeterministicallyActivatesAndWarmsLowestIdRow()
    {
        await SeedSettingsAsync("first-user", "first-model", "first-token");
        await SeedSettingsAsync("second-user", "second-model", "second-token");
        (AiSettingsStartupService service, FakeLlmRuntimeManager runtime, FakeChatClient chat) = CreateService();
        chat.EnqueueResponseText("hello");

        bool result = await service.ActivateFirstAndWarmUpAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(runtime.IsConfigured);
        Assert.Equal("first-model", runtime.GetRequiredSnapshot().Options.ModelId);
        Assert.Equal("first-token", runtime.GetRequiredSnapshot().Options.ApiKey);
        RecordedCall call = Assert.Single(chat.Calls);
        Assert.Equal("hi", Assert.Single(call.Messages).Text);
        Assert.Equal(1, call.Options?.MaxOutputTokens);
    }

    [Fact]
    public async Task StartupWarmupFailureRetainsConfiguredClient()
    {
        await SeedSettingsAsync("alice", "configured-model", "token");
        (AiSettingsStartupService service, FakeLlmRuntimeManager runtime, _) = CreateService();
        runtime.WarmUpException = new LlmConnectionException("Service unavailable.");

        bool result = await service.ActivateFirstAndWarmUpAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.True(runtime.IsConfigured);
        Assert.Equal("configured-model", runtime.GetRequiredSnapshot().Options.ModelId);
    }

    [Fact]
    public async Task UpgradeEncryptsLegacyPlaintextExactlyOnceAndKeepsItUsable()
    {
        AiSettings row = await SeedSettingsAsync("alice", "model", "legacy-token", protect: false);
        (AiSettingsStartupService service, _, _) = CreateService();

        await service.UpgradePlaintextTokensAsync(TestContext.Current.CancellationToken);
        string firstCiphertext = (await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken)).ApiKeyCiphertext;
        await service.UpgradePlaintextTokensAsync(TestContext.Current.CancellationToken);
        string secondCiphertext = (await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken)).ApiKeyCiphertext;

        Assert.NotEqual("legacy-token", row.ApiKeyCiphertext);
        Assert.StartsWith("dp:v1:", firstCiphertext);
        Assert.Equal(firstCiphertext, secondCiphertext);
        Assert.Equal("legacy-token", _protector.Unprotect(secondCiphertext));
    }

    private (AiSettingsStartupService Service, FakeLlmRuntimeManager Runtime, FakeChatClient Chat) CreateService()
    {
        FakeChatClient chat = new();
        FakeLlmRuntimeManager runtime = new(new AIOptions
        {
            ModelId = "unused",
            EndpointUrl = "http://localhost:1234/v1/",
        }, chat)
        {
            IsConfigured = false,
        };
        LoadLLMUseCase load = new(runtime);
        AiSettingsStartupService service = new(
            Db,
            _protector,
            runtime,
            load,
            NullLogger<AiSettingsStartupService>.Instance);
        return (service, runtime, chat);
    }

    private async Task<AiSettings> SeedSettingsAsync(
        string username,
        string model,
        string token,
        bool protect = true)
    {
        User user = new()
        {
            Username = username,
            PasswordHash = "hash",
            Fido2Id = [1, 2, 3],
            Preferences = new UserPreferences { Prompt = "prompt", Interests = ["AI"] },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        AiSettings settings = new()
        {
            ModelId = model,
            EndpointUrl = "http://localhost:1234/v1/",
            ApiKeyCiphertext = protect ? _protector.Protect(token) : token,
            UserPreferencesId = user.Preferences!.Id,
            UserPreferences = user.Preferences,
        };
        Db.AiSettings.Add(settings);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return settings;
    }
}
