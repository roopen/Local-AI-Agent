using LocalAIAgent.Application.News.AI;

namespace LocalAIAgent.Tests.UnitTests.Parsers;

public class ExpandedNewsResultTests
{
    [Fact]
    public void FromJson_PlainObject_DeserializesAllFields()
    {
        const string json = """{"ArticleWasTranslated":true,"Translation":"Hello","TermsAndExplanations":[]}""";

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.True(result.ArticleWasTranslated);
        Assert.Equal("Hello", result.Translation);
    }

    [Fact]
    public void FromJson_ObjectWithLeadingAndTrailingNoise_ExtractsObject()
    {
        const string json = """
            here is my answer:
            {"ArticleWasTranslated":false,"Translation":null,"TermsAndExplanations":[]}
            hope that helps.
            """;

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.False(result.ArticleWasTranslated);
        Assert.Null(result.Translation);
    }

    [Fact]
    public void FromJson_NullInput_ReturnsDefaultInstance()
    {
        ExpandedNewsResult result = ExpandedNewsResult.FromJson(null);

        Assert.False(result.ArticleWasTranslated);
        Assert.Null(result.Translation);
        Assert.Empty(result.TermsAndExplanations);
    }

    [Fact]
    public void FromJson_EmptyString_ReturnsDefaultInstance()
    {
        ExpandedNewsResult result = ExpandedNewsResult.FromJson(string.Empty);

        Assert.False(result.ArticleWasTranslated);
        Assert.Empty(result.TermsAndExplanations);
    }

    [Fact]
    public void FromJson_NoBraces_ReturnsDefaultInstance()
    {
        ExpandedNewsResult result = ExpandedNewsResult.FromJson("no JSON here at all");

        Assert.Empty(result.TermsAndExplanations);
    }

    [Fact]
    public void FromJson_NestedObject_IsExtractedAsTopLevel()
    {
        // The outer balanced {} is the JSON we want; the inner {} is a property value.
        const string json = """
            {"ArticleWasTranslated":true,"Translation":"x","TermsAndExplanations":[{"Key":{"Term":"AI"},"Value":{"Explanation":"artificial intelligence"}}]}
            """;

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.True(result.ArticleWasTranslated);
        KeyValuePair<TermString, ExplanationString> entry = Assert.Single(result.TermsAndExplanations);
        Assert.Equal("AI", entry.Key.Term);
        Assert.Equal("artificial intelligence", entry.Value.Explanation);
    }

    [Fact]
    public void FromJson_MultipleObjects_PicksFirstBalancedOne()
    {
        // The state machine returns as soon as the first object closes,
        // so the second object's data should never appear.
        const string json = """
            {"ArticleWasTranslated":true,"Translation":"first","TermsAndExplanations":[]}
            {"ArticleWasTranslated":false,"Translation":"second","TermsAndExplanations":[]}
            """;

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.True(result.ArticleWasTranslated);
        Assert.Equal("first", result.Translation);
    }

    [Fact]
    public void FromJson_BraceInStringValue_DoesNotConfuseStateMachine()
    {
        // The '{' inside the Translation string must NOT increment depth tracking.
        const string json = """{"ArticleWasTranslated":true,"Translation":"a { brace in text","TermsAndExplanations":[]}""";

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.Equal("a { brace in text", result.Translation);
    }

    [Fact]
    public void FromJson_EscapedQuoteInString_DoesNotEndStringEarly()
    {
        // The \" must not be treated as a string terminator.
        const string json = """{"ArticleWasTranslated":false,"Translation":"he said \"hi\" politely","TermsAndExplanations":[]}""";

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        Assert.Equal("""he said "hi" politely""", result.Translation);
    }

    [Fact]
    public void FromJson_UnclosedObject_ReturnsDefaultInstance()
    {
        // No matching '}' for the opening '{', state machine never finds depth==0 again.
        const string json = """{"ArticleWasTranslated":true,"Translation":"oops""";

        ExpandedNewsResult result = ExpandedNewsResult.FromJson(json);

        // Falls through to JsonSerializer.Deserialize(null/empty), which returns a default.
        Assert.False(result.ArticleWasTranslated);
        Assert.Null(result.Translation);
    }
}
