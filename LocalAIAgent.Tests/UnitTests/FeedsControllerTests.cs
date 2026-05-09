using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Application.News;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class FeedsControllerTests : InMemoryDbTestBase
{
    private sealed class StubFeedCatalog(params FeedDescriptor[] feeds) : IFeedCatalog
    {
        public IReadOnlyList<FeedDescriptor> GetAllFeeds() => feeds;
    }

    private async Task<InfraModels.UserPreferences> SeedUserAsync(List<string>? disabled = null)
    {
        InfraModels.User user = new()
        {
            Username = "alice",
            PasswordHash = "h",
            Fido2Id = [1],
            Preferences = new InfraModels.UserPreferences
            {
                Prompt = "p",
                Interests = [],
                Dislikes = [],
                DisabledFeedSources = disabled ?? [],
            },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Preferences!;
    }

    [Fact]
    public async Task GetFeeds_ReturnsAllFeedsWithPerUserToggleState()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync(disabled: ["BloombergClient"]);
        StubFeedCatalog catalog = new(
            new FeedDescriptor("BloombergClient", "Bloomberg", "en"),
            new FeedDescriptor("NipponHōsōKyōkaiClient", "NHK", "ja"));

        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(prefs.UserId);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        List<FeedDto> feeds = Assert.IsAssignableFrom<List<FeedDto>>(ok.Value);
        Assert.Equal(2, feeds.Count);

        FeedDto bloomberg = feeds.Single(f => f.ClientName == "BloombergClient");
        Assert.False(bloomberg.Enabled);
        Assert.Equal("English", bloomberg.LanguageName);

        FeedDto nhk = feeds.Single(f => f.ClientName == "NipponHōsōKyōkaiClient");
        Assert.True(nhk.Enabled);
        Assert.Equal("Japanese", nhk.LanguageName);
    }

    [Fact]
    public async Task GetFeeds_UnknownUser_ReturnsNotFound()
    {
        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(userId: 9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Toggle_DisablingFeed_AddsClientNameToDisabledList()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        IActionResult result = await sut.Toggle(new ToggleFeedDto
        {
            UserId = prefs.UserId,
            ClientName = "BloombergClient",
            Enabled = false,
        });

        Assert.IsType<OkResult>(result);
        InfraModels.UserPreferences reloaded = await Db.UserPreferences.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Contains("BloombergClient", reloaded.DisabledFeedSources);
    }

    [Fact]
    public async Task Toggle_EnablingPreviouslyDisabledFeed_RemovesItFromList()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync(disabled: ["BloombergClient"]);
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        await sut.Toggle(new ToggleFeedDto
        {
            UserId = prefs.UserId,
            ClientName = "BloombergClient",
            Enabled = true,
        });

        InfraModels.UserPreferences reloaded = await Db.UserPreferences.SingleAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("BloombergClient", reloaded.DisabledFeedSources);
    }

    [Fact]
    public async Task Toggle_UnknownClientName_ReturnsBadRequest()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        IActionResult result = await sut.Toggle(new ToggleFeedDto
        {
            UserId = prefs.UserId,
            ClientName = "NotARealClient",
            Enabled = false,
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Toggle_UnknownUser_ReturnsNotFound()
    {
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        IActionResult result = await sut.Toggle(new ToggleFeedDto
        {
            UserId = 9999,
            ClientName = "BloombergClient",
            Enabled = false,
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // -------- Custom feeds --------

    [Fact]
    public async Task GetFeeds_IncludesCustomFeedsAfterBuiltIns()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        Db.CustomFeeds.Add(new InfraModels.CustomFeed
        {
            UserPreferencesId = prefs.Id,
            Url = "https://example.com/rss",
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog, new CustomFeedRepository(Db));

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(prefs.UserId);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        List<FeedDto> feeds = Assert.IsAssignableFrom<List<FeedDto>>(ok.Value);

        Assert.Equal(2, feeds.Count);
        FeedDto custom = feeds.Single(f => f.IsCustom);
        Assert.Equal("My Blog", custom.DisplayName);
        Assert.Equal("https://example.com/rss", custom.Url);
        Assert.NotNull(custom.CustomFeedId);
        Assert.True(custom.Enabled);
        Assert.False(feeds.Single(f => !f.IsCustom).IsCustom);
    }

    [Fact]
    public async Task AddCustom_PersistsAndReturnsTheCreatedFeedDto()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Url = "https://example.com/rss",
            DisplayName = "My Blog",
            Language = "ja",
        });

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        FeedDto created = Assert.IsType<FeedDto>(ok.Value);
        Assert.True(created.IsCustom);
        Assert.Equal("ja", created.Language);
        Assert.Equal("Japanese", created.LanguageName);
        Assert.Single(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddCustom_InvalidUrl_ReturnsBadRequest()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Url = "not-a-url",
            DisplayName = "Bad",
            Language = "en",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddCustom_UnsupportedLanguage_ReturnsBadRequest()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Url = "https://example.com/rss",
            DisplayName = "Klingon",
            Language = "tlh",
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddCustom_DuplicateUrl_ReturnsConflict()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId, Url = "https://example.com/rss", DisplayName = "First", Language = "en",
        });

        ActionResult<FeedDto> second = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId, Url = "https://example.com/rss", DisplayName = "Second", Language = "en",
        });

        Assert.IsType<ConflictObjectResult>(second.Result);
    }

    [Fact]
    public async Task RemoveCustom_DeletesFeedForOwningUser()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        InfraModels.CustomFeed feed = new()
        {
            UserPreferencesId = prefs.Id,
            Url = "https://example.com/rss",
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        };
        Db.CustomFeeds.Add(feed);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        IActionResult result = await sut.RemoveCustom(feed.Id, prefs.UserId);

        Assert.IsType<OkResult>(result);
        Assert.Empty(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Toggle_WithCustomClientNamePrefix_FlipsEnabledOnTheCustomFeed()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        InfraModels.CustomFeed feed = new()
        {
            UserPreferencesId = prefs.Id,
            Url = "https://example.com/rss",
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        };
        Db.CustomFeeds.Add(feed);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        FeedsController sut = new(Db, new StubFeedCatalog(), new CustomFeedRepository(Db));

        IActionResult result = await sut.Toggle(new ToggleFeedDto
        {
            UserId = prefs.UserId,
            ClientName = "custom:" + feed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Enabled = false,
        });

        Assert.IsType<OkResult>(result);
        InfraModels.CustomFeed reloaded = await Db.CustomFeeds.SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(reloaded.Enabled);
    }
}
