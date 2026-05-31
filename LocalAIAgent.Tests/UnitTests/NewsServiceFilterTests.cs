using LocalAIAgent.Application.News;
using LocalAIAgent.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net.Http;
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

    private static NewsItem BuildItemWithSource(string title, string summary, DateTimeOffset publishDate, string sourceClientName)
    {
        SyndicationItem item = new()
        {
            Title = new TextSyndicationContent(title),
            Summary = new TextSyndicationContent(summary),
            PublishDate = publishDate,
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/" + Guid.NewGuid())));
        return new NewsItem(item, sourceClientName);
    }

    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now.AddDays(-1);

    private static NewsService BuildService() =>
        new(Mock.Of<IHttpClientFactory>(), [], TimeProvider.System, NullLogger<NewsService>.Instance);

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

    [Fact]
    public void FilterNews_DropsItemsFromDisabledSources()
    {
        List<NewsItem> input =
        [
            BuildItemWithSource("Bloomberg headline", "summary", Now, "BloombergClient"),
            BuildItemWithSource("Reuters headline", "summary", Now, "ReutersClient"),
        ];
        HashSet<string> disabled = new(["BloombergClient"], StringComparer.OrdinalIgnoreCase);

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: [], Cutoff, disabled);

        NewsItem only = Assert.Single(filtered);
        Assert.Equal("Reuters headline", only.Title);
    }

    [Fact]
    public void FilterNews_KeepsItemsWithNullSourceClientName_EvenWhenDisabledSetGiven()
    {
        // Legacy or unattributed items shouldn't be dropped silently.
        List<NewsItem> input = [BuildItem("Untagged article", "summary", Now)];
        HashSet<string> disabled = new(["BloombergClient"], StringComparer.OrdinalIgnoreCase);

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: [], Cutoff, disabled);

        Assert.Single(filtered);
    }

    [Fact]
    public void FilterNews_DisabledSourceSetIsCaseInsensitive()
    {
        List<NewsItem> input = [BuildItemWithSource("Bloomberg headline", "summary", Now, "BloombergClient")];
        HashSet<string> disabled = new(["bloombergCLIENT"], StringComparer.OrdinalIgnoreCase);

        List<NewsItem> filtered = NewsService.FilterNews(input, dislikes: [], Cutoff, disabled);

        Assert.Empty(filtered);
    }

    [Fact]
    public void EvaluateFeedKeywords_InterestMatch_LeavesArticleUnresolved()
    {
        UserPreferences prefs = new()
        {
            Id = 7,
            Prompt = "Be helpful.",
            Interests = ["AI"],
            Dislikes = [],
        };
        NewsItem item = BuildItem("AI breakthrough", "summary", Now, categories: ["AI"]);

        FeedKeywordEvaluationResult result = BuildService().EvaluateFeedKeywords([item], prefs, includeReasoning: false);

        Assert.Empty(result.EvaluatedArticles);
        Assert.Same(item, Assert.Single(result.UnresolvedArticles));
    }

    [Fact]
    public void EvaluateFeedKeywords_DislikeMatch_ReturnsLowArticle()
    {
        UserPreferences prefs = new()
        {
            Id = 7,
            Prompt = "Be helpful.",
            Interests = ["AI"],
            Dislikes = ["Crypto"],
        };
        NewsItem item = BuildItem("AI crypto crossover", "summary", Now, categories: ["AI", "Crypto"]);

        FeedKeywordEvaluationResult result = BuildService().EvaluateFeedKeywords([item], prefs, includeReasoning: false);

        Assert.Empty(result.UnresolvedArticles);
        NewsArticle only = Assert.Single(result.EvaluatedArticles);
        Assert.Equal(Relevancy.Low, only.Relevancy);
        Assert.Null(only.Topic);
    }

    [Fact]
    public void EvaluateFeedKeywords_NoFeedKeywordMatch_LeavesArticleUnresolved()
    {
        UserPreferences prefs = new()
        {
            Id = 7,
            Prompt = "Be helpful.",
            Interests = ["AI"],
            Dislikes = ["Crypto"],
        };
        NewsItem item = BuildItem("Robotics update", "summary", Now, categories: ["Robotics"]);

        FeedKeywordEvaluationResult result = BuildService().EvaluateFeedKeywords([item], prefs, includeReasoning: false);

        Assert.Empty(result.EvaluatedArticles);
        Assert.Same(item, Assert.Single(result.UnresolvedArticles));
    }
}
