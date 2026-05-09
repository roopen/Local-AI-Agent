using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.Tests.UnitTests;

/// <summary>
/// Direct-call tests for <see cref="UserPreferencesController"/> against an in-memory SQLite DB.
/// These cover bugs the audit surfaced:
///   1) SaveAiSettings used .FirstOrDefault() with no filter — always wrote to whichever user was first.
///   2) The new-AiSettings branch did not set TopP.
/// </summary>
public class UserPreferencesControllerTests : InMemoryDbTestBase
{
    private async Task<User> SeedUserWithPreferencesAsync(string username, string prompt = "be helpful")
    {
        User user = new()
        {
            Username = username,
            PasswordHash = "hash",
            Fido2Id = [1, 2, 3],
            Preferences = new UserPreferences
            {
                Prompt = prompt,
                Interests = ["AI"],
                Dislikes = [],
            },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static AiSettingsDto MakeDto(int userId, decimal topP = 0.95m, string modelId = "test-model") => new()
    {
        UserId = userId,
        ModelId = modelId,
        EndpointUrl = "http://localhost:1234/v1/",
        Temperature = 0.5m,
        TopP = topP,
        FrequencyPenalty = 0.0m,
        PresencePenalty = 0.0m,
    };

    [Fact]
    public async Task SaveAiSettings_TwoUsers_WritesOnlyToUserMatchingDtoUserId()
    {
        // Arrange — seed two users; the bug used to grab the FIRST user regardless of DTO.
        User first = await SeedUserWithPreferencesAsync("alice");
        User second = await SeedUserWithPreferencesAsync("bob");

        UserPreferencesController controller = new(Db);

        // Act — request settings for the second user.
        IActionResult result = await controller.SaveAiSettings(MakeDto(second.Id, modelId: "bob-model"));

        // Assert — the response is OK and only the second user got AI settings.
        Assert.IsType<OkResult>(result);

        AiSettings? bobsSettings = await Db.AiSettings
            .FirstOrDefaultAsync(a => a.UserPreferencesId == second.Preferences!.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(bobsSettings);
        Assert.Equal("bob-model", bobsSettings.ModelId);

        AiSettings? alicesSettings = await Db.AiSettings
            .FirstOrDefaultAsync(a => a.UserPreferencesId == first.Preferences!.Id, TestContext.Current.CancellationToken);
        Assert.Null(alicesSettings);
    }

    [Fact]
    public async Task SaveAiSettings_CreateBranch_PersistsAllFieldsIncludingTopP()
    {
        // Arrange
        User user = await SeedUserWithPreferencesAsync("alice");
        UserPreferencesController controller = new(Db);

        // Act
        IActionResult result = await controller.SaveAiSettings(MakeDto(user.Id, topP: 0.42m));

        // Assert — the create branch must have set TopP (regression for the missing-TopP bug).
        Assert.IsType<OkResult>(result);
        AiSettings settings = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0.42m, settings.TopP);
        Assert.Equal("test-model", settings.ModelId);
        Assert.Equal(0.5m, settings.Temperature);
    }

    [Fact]
    public async Task SaveAiSettings_UpdateBranch_OverwritesExistingFieldsForRequestedUser()
    {
        // Arrange — seed user with existing AI settings
        User user = await SeedUserWithPreferencesAsync("alice");
        AiSettings existing = new()
        {
            ModelId = "old-model",
            EndpointUrl = "http://old/",
            Temperature = 0.1m,
            TopP = 0.1m,
            FrequencyPenalty = 0.1m,
            PresencePenalty = 0.1m,
            UserPreferencesId = user.Preferences!.Id,
            UserPreferences = user.Preferences,
        };
        Db.AiSettings.Add(existing);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        UserPreferencesController controller = new(Db);

        // Act
        await controller.SaveAiSettings(MakeDto(user.Id, topP: 0.88m, modelId: "new-model"));

        // Assert — the existing row was updated (no second row created).
        AiSettings settings = await Db.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("new-model", settings.ModelId);
        Assert.Equal(0.88m, settings.TopP);
    }

    [Fact]
    public async Task SaveAiSettings_UnknownUserId_ReturnsNotFound()
    {
        // Arrange — empty DB (no users)
        UserPreferencesController controller = new(Db);

        // Act
        IActionResult result = await controller.SaveAiSettings(MakeDto(userId: 9999));

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
