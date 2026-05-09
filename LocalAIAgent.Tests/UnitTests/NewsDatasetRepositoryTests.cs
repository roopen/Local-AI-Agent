using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Application.News;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class NewsDatasetRepositoryTests : InMemoryDbTestBase
{
    private readonly NewsDatasetRepository _sut;

    public NewsDatasetRepositoryTests()
    {
        _sut = new NewsDatasetRepository(Db);
    }

    private async Task<InfraModels.UserPreferences> SeedPreferencesAsync()
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

    private static NewsArticle Article(string link, Relevancy relevancy = Relevancy.High, string? topic = null) => new()
    {
        Title = $"title for {link}",
        Summary = $"summary for {link}",
        Link = link,
        Source = new Uri(link).DnsSafeHost,
        PublishedDate = DateTime.UtcNow,
        Categories = [],
        Relevancy = relevancy,
        Topic = topic,
    };

    [Fact]
    public async Task GetCachedEvaluationsAsync_ReturnsMatchingEntriesWithParsedRelevancy()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();
        Db.NewsEvaluationEntries.Add(new InfraModels.NewsEvaluationEntry
        {
            ArticleTitle = "t",
            ArticleSummary = "s",
            ArticleLink = "https://x.com/a",
            ArticleSource = "x.com",
            ArticleTopic = "Tech",
            Relevancy = "High",
            Reasoning = "because",
            ModelUsed = "model-1",
            UserPreferencesId = prefs.Id,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Dictionary<string, CachedNewsEvaluation> result = await _sut.GetCachedEvaluationsAsync(
            ["https://x.com/a", "https://x.com/missing"],
            TestContext.Current.CancellationToken);

        CachedNewsEvaluation entry = Assert.Single(result.Values);
        Assert.Equal(Relevancy.High, entry.Relevancy);
        Assert.Equal("Tech", entry.Topic);
        Assert.Equal("because", entry.Reasoning);
        Assert.Equal("model-1", entry.ModelUsed);
    }

    [Fact]
    public async Task GetCachedEvaluationsAsync_NoMatches_ReturnsEmpty()
    {
        await SeedPreferencesAsync();

        Dictionary<string, CachedNewsEvaluation> result = await _sut.GetCachedEvaluationsAsync(
            ["https://x.com/missing"],
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCachedEvaluationsAsync_UnparseableRelevancy_FallsBackToLow()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();
        Db.NewsEvaluationEntries.Add(new InfraModels.NewsEvaluationEntry
        {
            ArticleTitle = "t",
            ArticleSummary = "s",
            ArticleLink = "https://x.com/a",
            ArticleSource = "x.com",
            Relevancy = "Bogus",
            UserPreferencesId = prefs.Id,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Dictionary<string, CachedNewsEvaluation> result = await _sut.GetCachedEvaluationsAsync(
            ["https://x.com/a"],
            TestContext.Current.CancellationToken);

        Assert.Equal(Relevancy.Low, result["https://x.com/a"].Relevancy);
    }

    [Fact]
    public async Task SaveAsync_AddsNewEntriesWithModelMetadata()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();
        List<NewsArticle> articles = [Article("https://x.com/a", topic: "Tech"), Article("https://x.com/b", Relevancy.Low)];

        await _sut.SaveAsync(articles, prefs.Id, useInDataset: true, modelUsed: "model-1", TestContext.Current.CancellationToken);

        List<InfraModels.NewsEvaluationEntry> stored = await Db.NewsEvaluationEntries.OrderBy(e => e.ArticleLink).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.All(stored, e =>
        {
            Assert.Equal(prefs.Id, e.UserPreferencesId);
            Assert.True(e.UseInDataset);
            Assert.Equal("model-1", e.ModelUsed);
        });
        Assert.Equal("High", stored[0].Relevancy);
        Assert.Equal("Low", stored[1].Relevancy);
    }

    [Fact]
    public async Task SaveAsync_SkipsArticlesWithExistingLinks()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();
        Db.NewsEvaluationEntries.Add(new InfraModels.NewsEvaluationEntry
        {
            ArticleTitle = "preexisting",
            ArticleSummary = "s",
            ArticleLink = "https://x.com/dup",
            ArticleSource = "x.com",
            Relevancy = "High",
            UserPreferencesId = prefs.Id,
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<NewsArticle> articles = [Article("https://x.com/dup"), Article("https://x.com/new")];
        await _sut.SaveAsync(articles, prefs.Id, useInDataset: false, modelUsed: "m", TestContext.Current.CancellationToken);

        // The duplicate is untouched (still has its preexisting title), the new one is added.
        Assert.Equal(2, await Db.NewsEvaluationEntries.CountAsync(TestContext.Current.CancellationToken));
        InfraModels.NewsEvaluationEntry dup = await Db.NewsEvaluationEntries.SingleAsync(e => e.ArticleLink == "https://x.com/dup", TestContext.Current.CancellationToken);
        Assert.Equal("preexisting", dup.ArticleTitle);
    }

    [Fact]
    public async Task SaveAsync_NullModelUsed_DefaultsToUnknown()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();

        await _sut.SaveAsync([Article("https://x.com/a")], prefs.Id, useInDataset: false, modelUsed: null, TestContext.Current.CancellationToken);

        InfraModels.NewsEvaluationEntry stored = await Db.NewsEvaluationEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Unknown", stored.ModelUsed);
    }

    [Fact]
    public async Task SaveAsync_EmptyArticles_AddsNothing()
    {
        InfraModels.UserPreferences prefs = await SeedPreferencesAsync();

        await _sut.SaveAsync([], prefs.Id, useInDataset: false, modelUsed: "m", TestContext.Current.CancellationToken);

        Assert.Empty(await Db.NewsEvaluationEntries.ToListAsync(TestContext.Current.CancellationToken));
    }
}
