using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalAIAgent.Tests.IntegrationTests;

public sealed class LocalServerMigrationTests
{
    [Fact]
    public async Task Migration_PromotesOldestUserAndKeepsOwnersAiSettings()
    {
        await using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ServiceCollection services = new();
        services.AddDbContext<UserContext>(builder => builder.UseSqlite(connection));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        UserContext context = scope.ServiceProvider.GetRequiredService<UserContext>();
        IMigrator migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260812100039_EncryptAiSettingsApiKey", TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, Fido2Id, PasswordHash, Username) VALUES (5, X'01', 'h', 'owner'), (9, X'02', 'h', 'member');",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO UserPreferences (Id, DisabledFeedSources, Dislikes, Interests, Prompt, TargetLanguage, UserId) " +
            "VALUES (50, '[]', '[]', '[]', 'owner prompt', 'en', 5), (90, '[]', '[]', '[]', 'member prompt', 'en', 9);",
            TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AiSettings (Id, ApiKeyCiphertext, EndpointUrl, FrequencyPenalty, ModelId, PresencePenalty, Temperature, TopP, UserPreferencesId) " +
            "VALUES (20, 'owner-secret', 'http://owner/v1/', '1.0', 'owner-model', '1.0', '0.2', '1.0', 50), " +
            "(10, 'member-secret', 'http://member/v1/', '1.0', 'member-model', '1.0', '0.2', '1.0', 90);",
            TestContext.Current.CancellationToken);

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        List<User> users = await context.Users.OrderBy(u => u.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserRole.Owner, users[0].Role);
        Assert.Equal(UserRole.Member, users[1].Role);
        Assert.All(users, user => Assert.False(user.IsDisabled));

        AiSettings settings = await context.AiSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(settings.Id, settings.HostId);
        Assert.Equal("owner-model", settings.Name);
        Assert.Equal("owner-model", settings.ModelId);
        Assert.Equal("owner-secret", settings.ApiKeyCiphertext);
        Assert.False(settings.UseResultsForDataset);
        Assert.Empty(await context.Invitations.ToListAsync(TestContext.Current.CancellationToken));
    }
}
