using LocalAIAgent.Application.News;

namespace LocalAIAgent.Tests.UnitTests;

public class NewsItemHtmlDecodingTests
{
    [Fact]
    public void GetDecodedHtmlString_DecodesNamedEntities()
    {
        Assert.Equal("AT&T announces", NewsItem.GetDecodedHtmlString("AT&amp;T announces"));
    }

    [Fact]
    public void GetDecodedHtmlString_DecodesNumericEntities()
    {
        Assert.Equal("café", NewsItem.GetDecodedHtmlString("caf&#233;"));
    }

    [Fact]
    public void GetDecodedHtmlString_StripsSimpleTags()
    {
        Assert.Equal("hello world", NewsItem.GetDecodedHtmlString("<p>hello world</p>"));
    }

    [Fact]
    public void GetDecodedHtmlString_StripsTagsWithAttributes()
    {
        Assert.Equal("click here", NewsItem.GetDecodedHtmlString("""<a href="https://x.com" target="_blank">click here</a>"""));
    }

    [Fact]
    public void GetDecodedHtmlString_StripsSelfClosingTags()
    {
        Assert.Equal("line1line2", NewsItem.GetDecodedHtmlString("line1<br/>line2"));
    }

    [Fact]
    public void GetDecodedHtmlString_HandlesMixedEntitiesAndTags()
    {
        Assert.Equal("Q&A: text", NewsItem.GetDecodedHtmlString("<h2>Q&amp;A:</h2> <em>text</em>"));
    }

    [Fact]
    public void GetDecodedHtmlString_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NewsItem.GetDecodedHtmlString(string.Empty));
    }

    [Fact]
    public void GetDecodedHtmlString_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NewsItem.GetDecodedHtmlString(null));
    }

    [Fact]
    public void GetDecodedHtmlString_WhitespaceInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NewsItem.GetDecodedHtmlString("   "));
    }

    [Fact]
    public void GetDecodedHtmlString_PlainTextWithoutMarkup_PassesThrough()
    {
        Assert.Equal("Just plain text.", NewsItem.GetDecodedHtmlString("Just plain text."));
    }
}
