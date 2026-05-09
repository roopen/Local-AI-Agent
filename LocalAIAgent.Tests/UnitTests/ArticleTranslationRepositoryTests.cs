using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Application.News;
using LocalAIAgent.Domain;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class ArticleTranslationRepositoryTests : InMemoryDbTestBase
{
    private readonly ArticleTranslationRepository _sut;

    public ArticleTranslationRepositoryTests()
    {
        _sut = new ArticleTranslationRepository(Db);
    }

    private static NewsArticle Article(string link, string title = "translated title", string summary = "translated summary") => new()
    {
        Title = title,
        Summary = summary,
        Link = link,
        Source = new Uri(link).DnsSafeHost,
        PublishedDate = DateTime.UtcNow,
        Categories = [],
        Relevancy = Relevancy.High,
    };

    [Fact]
    public async Task GetCachedTranslationsAsync_ReturnsMatchingLinksAndLanguage()
    {
        Db.ArticleTranslations.AddRange(
            new InfraModels.ArticleTranslation
            {
                ArticleLink = "https://x.com/a",
                OriginalTitle = "Hola", OriginalSummary = "...",
                TranslatedTitle = "Hello", TranslatedSummary = "world",
                TargetLanguage = "English",
            },
            new InfraModels.ArticleTranslation
            {
                ArticleLink = "https://x.com/b",
                OriginalTitle = "Bonjour", OriginalSummary = "...",
                TranslatedTitle = "Hello B", TranslatedSummary = "world B",
                TargetLanguage = "English",
            });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Dictionary<string, CachedTranslation> result = await _sut.GetCachedTranslationsAsync(
            ["https://x.com/a", "https://x.com/missing"],
            "English",
            TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("Hello", result["https://x.com/a"].Title);
        Assert.Equal("world", result["https://x.com/a"].Summary);
    }

    [Fact]
    public async Task GetCachedTranslationsAsync_FiltersByTargetLanguage()
    {
        // NOTE: ArticleLink has a unique constraint at the DB level, so the same link
        // cannot have two translations even in different languages — likely a schema
        // bug, since the application-level cache key is (link, language). Two distinct
        // links demonstrate the language filter without tripping the unique index.
        Db.ArticleTranslations.AddRange(
            new InfraModels.ArticleTranslation
            {
                ArticleLink = "https://x.com/english",
                OriginalTitle = "Hola", OriginalSummary = "...",
                TranslatedTitle = "Hello", TranslatedSummary = "world",
                TargetLanguage = "English",
            },
            new InfraModels.ArticleTranslation
            {
                ArticleLink = "https://x.com/spanish",
                OriginalTitle = "Bonjour", OriginalSummary = "...",
                TranslatedTitle = "Hola", TranslatedSummary = "mundo",
                TargetLanguage = "Spanish",
            });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Dictionary<string, CachedTranslation> result = await _sut.GetCachedTranslationsAsync(
            ["https://x.com/english", "https://x.com/spanish"],
            "Spanish",
            TestContext.Current.CancellationToken);

        // Only the Spanish row matches.
        Assert.Single(result);
        Assert.Equal("Hola", result["https://x.com/spanish"].Title);
    }

    [Fact]
    public async Task GetCachedTranslationsAsync_NoMatches_ReturnsEmpty()
    {
        Dictionary<string, CachedTranslation> result = await _sut.GetCachedTranslationsAsync(
            ["https://x.com/none"],
            "Spanish",
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveTranslationsAsync_AddsNewEntriesWithOriginalsAndTranslations()
    {
        List<NewsArticle> articles =
        [
            Article("https://x.com/a", title: "Hello", summary: "world"),
            Article("https://x.com/b", title: "Bye", summary: "later"),
        ];
        List<(string OriginalTitle, string OriginalSummary)> originals =
        [
            ("Hola", "mundo"),
            ("Adiós", "luego"),
        ];

        await _sut.SaveTranslationsAsync(articles, originals, "English", TestContext.Current.CancellationToken);

        List<InfraModels.ArticleTranslation> stored = await Db.ArticleTranslations.OrderBy(t => t.ArticleLink).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.Equal("Hola", stored[0].OriginalTitle);
        Assert.Equal("Hello", stored[0].TranslatedTitle);
        Assert.Equal("English", stored[0].TargetLanguage);
        Assert.Equal("Adiós", stored[1].OriginalTitle);
    }

    [Fact]
    public async Task SaveTranslationsAsync_SkipsLinksAlreadyInTable()
    {
        Db.ArticleTranslations.Add(new InfraModels.ArticleTranslation
        {
            ArticleLink = "https://x.com/a",
            OriginalTitle = "old original", OriginalSummary = "...",
            TranslatedTitle = "old translation", TranslatedSummary = "...",
            TargetLanguage = "English",
        });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<NewsArticle> articles = [Article("https://x.com/a", title: "new translation"), Article("https://x.com/b")];
        List<(string OriginalTitle, string OriginalSummary)> originals = [("orig a", "..."), ("orig b", "...")];

        await _sut.SaveTranslationsAsync(articles, originals, "English", TestContext.Current.CancellationToken);

        // Existing row is untouched, new row added.
        Assert.Equal(2, await Db.ArticleTranslations.CountAsync(TestContext.Current.CancellationToken));
        InfraModels.ArticleTranslation existing = await Db.ArticleTranslations.SingleAsync(t => t.ArticleLink == "https://x.com/a", TestContext.Current.CancellationToken);
        Assert.Equal("old translation", existing.TranslatedTitle);
    }
}
