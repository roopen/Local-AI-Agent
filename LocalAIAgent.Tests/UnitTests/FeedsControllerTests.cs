using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
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

        FeedsController sut = new(Db, catalog);

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
        FeedsController sut = new(Db, new StubFeedCatalog());

        ActionResult<List<FeedDto>> result = await sut.GetFeeds(userId: 9999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Toggle_DisablingFeed_AddsClientNameToDisabledList()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync();
        StubFeedCatalog catalog = new(new FeedDescriptor("BloombergClient", "Bloomberg", "en"));
        FeedsController sut = new(Db, catalog);

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
        FeedsController sut = new(Db, catalog);

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
        FeedsController sut = new(Db, catalog);

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
        FeedsController sut = new(Db, catalog);

        IActionResult result = await sut.Toggle(new ToggleFeedDto
        {
            UserId = 9999,
            ClientName = "BloombergClient",
            Enabled = false,
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
