using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Diagnostics.Metrics;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class NewsControllerFeedbackTests : InMemoryDbTestBase
{
    private readonly NewsController _sut;
    private readonly ServiceProvider _metricsProvider;

    public NewsControllerFeedbackTests()
    {
        // Real IMeterFactory — Feedback doesn't touch metrics, but the ctor demands one.
        _metricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        NewsMetrics metrics = new(_metricsProvider.GetRequiredService<IMeterFactory>());

        _sut = new NewsController(
            Mock.Of<INewsChatUseCase>(),
            Mock.Of<IGetDatasetUseCase>(),
            metrics,
            Db);
    }

    public override void Dispose()
    {
        _metricsProvider.Dispose();
        base.Dispose();
    }

    private async Task<InfraModels.UserPreferences> SeedUserAsync(string username)
    {
        InfraModels.User user = new()
        {
            Username = username,
            PasswordHash = "h",
            Fido2Id = [1],
            Preferences = new InfraModels.UserPreferences { Prompt = "p", Interests = [], Dislikes = [] },
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Preferences!;
    }

    private static NewsFeedbackDto MakeDto(int userId, string link, bool liked = true, string? reason = "I like AI") => new()
    {
        UserId = userId,
        ArticleLink = link,
        ArticleTitle = "AI breakthrough",
        ArticleSummary = "summary",
        ArticleTopic = "Tech",
        IsLiked = liked,
        Reason = reason,
    };

    [Fact]
    public async Task SubmitFeedback_NoPreferencesForUser_ReturnsNotFound()
    {
        // Empty DB — no user, no preferences.
        IActionResult result = await _sut.SubmitFeedback(MakeDto(userId: 1, link: "https://x.com/a"));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SubmitFeedback_FirstFeedbackForArticle_CreatesNewEntry()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync("alice");

        IActionResult result = await _sut.SubmitFeedback(MakeDto(prefs.UserId, "https://x.com/a", liked: true));

        Assert.IsType<OkResult>(result);
        InfraModels.NewsEvaluationEntry entry = await Db.NewsEvaluationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("High", entry.Relevancy);
        Assert.Equal(prefs.Id, entry.UserPreferencesId);
        Assert.Equal("I like AI", entry.Reasoning);
    }

    [Fact]
    public async Task SubmitFeedback_ExistingEntryForArticle_UpdatesRelevancyAndReason()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync("alice");
        Db.NewsEvaluationEntries.Add(new InfraModels.NewsEvaluationEntry
        {
            ArticleTitle = "old",
            ArticleSummary = "old",
            ArticleSource = "x.com",
            ArticleLink = "https://x.com/a",
            Relevancy = "Low",
            Reasoning = "old reason",
            UserPreferencesId = prefs.Id,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Flip the verdict to liked.
        await _sut.SubmitFeedback(MakeDto(prefs.UserId, "https://x.com/a", liked: true, reason: "changed my mind"));

        // No second row added — the existing entry was updated in place.
        InfraModels.NewsEvaluationEntry entry = await Db.NewsEvaluationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("High", entry.Relevancy);
        Assert.Equal("changed my mind", entry.Reasoning);
    }

    [Fact]
    public async Task SubmitFeedback_IsLikedFalse_StoresLowRelevancy()
    {
        InfraModels.UserPreferences prefs = await SeedUserAsync("alice");

        await _sut.SubmitFeedback(MakeDto(prefs.UserId, "https://x.com/a", liked: false));

        InfraModels.NewsEvaluationEntry entry = await Db.NewsEvaluationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Low", entry.Relevancy);
    }
}
