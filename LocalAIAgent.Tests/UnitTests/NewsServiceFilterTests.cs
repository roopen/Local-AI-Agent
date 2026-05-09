using LocalAIAgent.Application.News;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.Tests.UnitTests;

/// <summary>
/// Tests the offline filtering surfaces of <see cref="NewsService"/>.
/// The HTTP-fed loader path used to be smoke-tested via real RSS feeds, but that
/// made the suite slow and flaky and gave little signal beyond "did the network work".
/// </summary>
public class NewsServiceFilterTests
{
    private static NewsItem BuildItem(string title, string summary, DateTimeOffset publishDate, string? link = null, params string[] categories)
    {
        SyndicationItem item = new()
        {
            Title = new TextSyndicationContent(title),
            Summary = new TextSyndicationContent(summary),
            PublishDate = publishDate,
        };
        item.Links.Add(new SyndicationLink(new Uri(link ?? "https://example.com/" + Guid.NewGuid())));
        foreach (string category in categories)
            item.Categories.Add(new SyndicationCategory(category));
        return new NewsItem(item);
    }

    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now.AddDays(-1);

    [Fact]
    public void FilterNews_KeepsItemsAtOrAfterCutoff()
    {
        List<NewsItem> input =
        [
            BuildItem("Today", "fresh", Now),
            BuildItem("Right at cutoff", "edge", Cutoff),
        ];

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: [], Cutoff);

        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void FilterNews_DropsItemsOlderThanCutoff()
    {
        List<NewsItem> input =
        [
            BuildItem("Stale", "old", Now.AddDays(-3)),
        ];

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: [], Cutoff);

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterNews_AppliesDislikeFilter()
    {
        List<NewsItem> input =
        [
            BuildItem("Tech update", "exciting AI news", Now),
            BuildItem("Sports recap", "weekend scores", Now),
        ];

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: ["sports"], Cutoff);

        NewsItem only = Assert.Single(filtered);
        Assert.Equal("Tech update", only.Title);
    }

    [Fact]
    public void PassesDislikeFilter_MatchesWordInTitle_RejectsItem()
    {
        NewsItem item = BuildItem("Sports recap", "scores", Now);

        Assert.False(NewsService.PassesDislikeFilter(item, ["sports"]));
    }

    [Fact]
    public void PassesDislikeFilter_MatchesWordInSummary_RejectsItem()
    {
        NewsItem item = BuildItem("Headline", "another sports update", Now);

        Assert.False(NewsService.PassesDislikeFilter(item, ["sports"]));
    }

    [Fact]
    public void PassesDislikeFilter_MatchesCategoryWord_RejectsItem()
    {
        NewsItem item = BuildItem("Headline", "summary", Now, link: null, "sports");

        Assert.False(NewsService.PassesDislikeFilter(item, ["sports"]));
    }

    [Fact]
    public void PassesDislikeFilter_IsCaseInsensitive()
    {
        NewsItem item = BuildItem("SPORTS update", "summary", Now);

        Assert.False(NewsService.PassesDislikeFilter(item, ["sports"]));
    }

    [Fact]
    public void PassesDislikeFilter_RequiresWholeWordMatch()
    {
        // The regex uses \b...\b boundaries — "transport" matches as a whole word and rejects,
        // but "sport" as a substring inside "transport" must NOT cause a false rejection.
        NewsItem item = BuildItem("Public transport plans", "summary", Now);

        Assert.False(NewsService.PassesDislikeFilter(item, ["transport"]));
        Assert.True(NewsService.PassesDislikeFilter(item, ["sport"]));
    }

    [Fact]
    public void PassesDislikeFilter_NoDislikes_KeepsAllItems()
    {
        NewsItem item = BuildItem("Anything", "anything", Now);

        Assert.True(NewsService.PassesDislikeFilter(item, []));
    }

    [Fact]
    public void PassesDislikeFilter_RegexSpecialCharsInDislike_AreEscaped()
    {
        // A naive regex would treat "." as a wildcard; Regex.Escape makes it literal.
        NewsItem item = BuildItem("Headline about cats", "summary", Now);

        Assert.True(NewsService.PassesDislikeFilter(item, ["c.ts"]));
    }
}
