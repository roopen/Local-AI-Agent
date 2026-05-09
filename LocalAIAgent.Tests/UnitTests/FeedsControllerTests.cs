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

    /// <summary>Validator stub used when validation isn't the focus of the test.</summary>
    private sealed class AlwaysValid : IFeedValidator
    {
        public Task<List<FeedUrlValidationResult>> ValidateAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default) =>
            Task.FromResult(urls.Select(u => new FeedUrlValidationResult(u, true, null)).ToList());
    }

    /// <summary>Validator stub that fails every URL with a fixed error.</summary>
    private sealed class AlwaysInvalid(string error) : IFeedValidator
    {
        public Task<List<FeedUrlValidationResult>> ValidateAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default) =>
            Task.FromResult(urls.Select(u => new FeedUrlValidationResult(u, false, error)).ToList());
    }

    private FeedsController BuildController(IFeedCatalog catalog, IFeedValidator? validator = null) =>
        new(Db, catalog, new CustomFeedRepository(Db), validator ?? new AlwaysValid());

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

        FeedsController sut = BuildController(catalog);

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
        FeedsController sut = BuildController(new StubFeedCatalog());

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(userId: 9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Toggle_DisablingFeed_AddsClientNameToDisabledList()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = BuildController(catalog);

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
        FeedsController sut = BuildController(catalog);

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
        FeedsController sut = BuildController(catalog);

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
        FeedsController sut = BuildController(catalog);

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
            Urls = ["https://example.com/rss"],
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = BuildController(catalog);

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(prefs.UserId);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        List<FeedDto> feeds = Assert.IsAssignableFrom<List<FeedDto>>(ok.Value);

        Assert.Equal(2, feeds.Count);
        FeedDto custom = feeds.Single(f => f.IsCustom);
        Assert.Equal("My Blog", custom.DisplayName);
        Assert.Equal(["https://example.com/rss"], custom.Urls);
        Assert.NotNull(custom.CustomFeedId);
        Assert.True(custom.Enabled);
        Assert.False(feeds.Single(f => !f.IsCustom).IsCustom);
    }

    [Fact]
    public async Task AddCustom_AllUrlsValid_PersistsAndReturnsTheCreatedFeedDto()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = BuildController(new StubFeedCatalog());

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Urls = ["https://example.com/rss", "https://example.com/rss2"],
            DisplayName = "My Blog",
            Language = "ja",
        }, TestContext.Current.CancellationToken);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        FeedDto created = Assert.IsType<FeedDto>(ok.Value);
        Assert.True(created.IsCustom);
        Assert.Equal("ja", created.Language);
        Assert.Equal("Japanese", created.LanguageName);
        Assert.NotNull(created.Urls);
        Assert.Equal(2, created.Urls!.Count);
        Assert.Single(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddCustom_ValidatorRejectsAUrl_ReturnsBadRequestWithUrlErrors_AndNothingPersisted()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = BuildController(new StubFeedCatalog(), new AlwaysInvalid("HTTP 404 Not Found"));

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Urls = ["https://example.com/rss"],
            DisplayName = "Bad",
            Language = "en",
        }, TestContext.Current.CancellationToken);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        AddCustomFeedErrorDto body = Assert.IsType<AddCustomFeedErrorDto>(bad.Value);
        Assert.NotNull(body.UrlErrors);
        Assert.Equal("HTTP 404 Not Found", body.UrlErrors!["https://example.com/rss"]);
        Assert.Empty(await Db.CustomFeeds.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddCustom_UnsupportedLanguage_ReturnsBadRequest()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = BuildController(new StubFeedCatalog());

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Urls = ["https://example.com/rss"],
            DisplayName = "Klingon",
            Language = "tlh",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddCustom_EmptyUrlsList_ReturnsBadRequest()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        FeedsController sut = BuildController(new StubFeedCatalog());

        ActionResult<FeedDto> result = await sut.AddCustom(new AddCustomFeedDto
        {
            UserId = prefs.UserId,
            Urls = [],
            DisplayName = "Empty",
            Language = "en",
        }, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RemoveCustom_DeletesFeedForOwningUser()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        InfraModels.CustomFeed feed = new()
        {
            UserPreferencesId = prefs.Id,
            Urls = ["https://example.com/rss"],
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        };
        Db.CustomFeeds.Add(feed);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        FeedsController sut = BuildController(new StubFeedCatalog());

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
            Urls = ["https://example.com/rss"],
            DisplayName = "My Blog",
            Language = "en",
            Enabled = true,
        };
        Db.CustomFeeds.Add(feed);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        FeedsController sut = BuildController(new StubFeedCatalog());

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
