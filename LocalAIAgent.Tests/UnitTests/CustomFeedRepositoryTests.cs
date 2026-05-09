using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Application.News;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class CustomFeedRepositoryTests : InMemoryDbTestBase
{
    private readonly CustomFeedRepository _sut;

    public CustomFeedRepositoryTests()
    {
        _sut = new CustomFeedRepository(Db);
    }

    private async Task<InfraModels.UserPreferences> SeedUserAsync()
    {
        InfraModels.User user = new()
        {
            Username = "alice",
            PasswordHash = "h",
            Fido2Id = [1],
            Preferences = new InfraModels.UserPreferences { Prompt = "p", Interests = [], Dislikes = [] },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Preferences!;
    }

    [Fact]
    public async Task AddAsync_PersistsFeedWithDefaultsAndReturnsDescriptor()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();

        CustomFeedDescriptor created = await _sut.AddAsync(
            prefs.Id, ["https://example.com/feed"], "Example", "en", TestContext.Current.CancellationToken);

        Assert.True(created.Id > 0);
        Assert.True(created.Enabled);
        Assert.Equal(["https://example.com/feed"], created.Urls);
        Assert.Equal("Example", created.DisplayName);
        Assert.Equal("en", created.Language);
        Assert.Single(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAsync_MultipleUrlsForSameFeed_AreAllPersisted()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();

        CustomFeedDescriptor created = await _sut.AddAsync(
            prefs.Id,
            ["https://feeds.bloomberg.com/markets/news.rss", "https://feeds.bloomberg.com/technology/news.rss"],
            "Bloomberg", "en", TestContext.Current.CancellationToken);

        Assert.Equal(2, created.Urls.Count);
        InfraModels.CustomFeed reloaded = await Db.CustomFeeds.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, reloaded.Urls.Count);
    }

    [Fact]
    public async Task AddAsync_DedupesAndTrimsUrls()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();

        CustomFeedDescriptor created = await _sut.AddAsync(
            prefs.Id,
            ["  https://example.com/feed  ", "https://example.com/feed", "https://example.com/other"],
            "Example", "en", TestContext.Current.CancellationToken);

        Assert.Equal(2, created.Urls.Count);
        Assert.Equal(["https://example.com/feed", "https://example.com/other"], created.Urls);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyThatUsersFeedsOrderedByDisplayName()
    {
        InfraModels.UserPreferences alice = await SeedUserAsync();
        InfraModels.User bob = new()
        {
            Username = "bob",
            PasswordHash = "h",
            Fido2Id = [2],
            Preferences = new InfraModels.UserPreferences { Prompt = "p", Interests = [], Dislikes = [] },
        };
        Db.Users.Add(bob);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _sut.AddAsync(alice.Id, ["https://example.com/zebra"], "Zebra", "en", TestContext.Current.CancellationToken);
        await _sut.AddAsync(alice.Id, ["https://example.com/apple"], "Apple", "en", TestContext.Current.CancellationToken);
        await _sut.AddAsync(bob.Preferences!.Id, ["https://example.com/bobs"], "Bobs", "en", TestContext.Current.CancellationToken);

        List<CustomFeedDescriptor> list = await _sut.GetForUserAsync(alice.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, list.Count);
        Assert.Equal("Apple", list[0].DisplayName);
        Assert.Equal("Zebra", list[1].DisplayName);
    }

    [Fact]
    public async Task RemoveAsync_DropsTheFeedScopedToOwner()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        CustomFeedDescriptor created = await _sut.AddAsync(prefs.Id, ["https://example.com/feed"], "Example", "en", TestContext.Current.CancellationToken);

        bool removed = await _sut.RemoveAsync(prefs.Id, created.Id, TestContext.Current.CancellationToken);

        Assert.True(removed);
        Assert.Empty(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_OtherUsersFeed_ReturnsFalse()
    {
        InfraModels.UserPreferences alice = await SeedUserAsync();
        CustomFeedDescriptor created = await _sut.AddAsync(alice.Id, ["https://example.com/feed"], "Example", "en", TestContext.Current.CancellationToken);

        bool removed = await _sut.RemoveAsync(userPreferencesId: alice.Id + 999, customFeedId: created.Id, TestContext.Current.CancellationToken);

        Assert.False(removed);
        Assert.Single(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetEnabledAsync_FlipsTheFlag()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        CustomFeedDescriptor created = await _sut.AddAsync(prefs.Id, ["https://example.com/feed"], "Example", "en", TestContext.Current.CancellationToken);

        bool result = await _sut.SetEnabledAsync(prefs.Id, created.Id, enabled: false, TestContext.Current.CancellationToken);

        Assert.True(result);
        InfraModels.CustomFeed reloaded = await Db.CustomFeeds.SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(reloaded.Enabled);
    }

    [Fact]
    public async Task RecordFetchResultAsync_WithError_StoresMessageAndTimestamp()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        CustomFeedDescriptor created = await _sut.AddAsync(prefs.Id, ["https://example.com/feed"], "Example", "en", TestContext.Current.CancellationToken);

        await _sut.RecordFetchResultAsync(created.Id, "DNS lookup failed", TestContext.Current.CancellationToken);

        InfraModels.CustomFeed reloaded = await Db.CustomFeeds.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("DNS lookup failed", reloaded.LastFetchErrorMessage);
        Assert.NotNull(reloaded.LastFetchedAt);
    }
}
