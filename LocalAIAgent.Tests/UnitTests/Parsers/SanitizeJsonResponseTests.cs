using LocalAIAgent.Application.News.AI;

namespace LocalAIAgent.Tests.UnitTests.Parsers;

public class SanitizeJsonResponseTests
{
    [Fact]
    public void Sanitize_AlreadyValidJson_PassesThrough()
    {
        const string json = """[{"title":"Hello","summary":"World"}]""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_ReplacesInvalidBackslashApostrophe()
    {
        // \' is not a valid JSON escape sequence; some models emit it.
        const string json = """{"title":"it\'s broken"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal("""{"title":"it's broken"}""", sanitized);
    }

    [Fact]
    public void Sanitize_PreservesAlreadyEscapedQuote()
    {
        // \" inside a string should remain as \"
        const string json = """{"title":"he said \"hi\" loudly"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_EscapesUnescapedQuoteInsideStringValue()
    {
        // The middle " is followed by 'b' (not :,}],EOF), so it should be escaped.
        const string input = """{"title":"a"b","summary":"x"}""";
        const string expected = """{"title":"a\"b","summary":"x"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(input);

        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void Sanitize_QuoteFollowedByColon_TreatedAsStringClose()
    {
        // " : is the typical key terminator pattern.
        const string json = """{"title":"value"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_QuoteFollowedByComma_TreatedAsStringClose()
    {
        const string json = """{"title":"a","other":"b"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_QuoteFollowedByCloseBrace_TreatedAsStringClose()
    {
        const string json = """{"title":"end"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_QuoteFollowedByCloseBracket_TreatedAsStringClose()
    {
        const string json = """["a","b"]""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_TolerantOfSpacesBeforeStructuralChar()
    {
        // The lookahead skips spaces, so " : and " , are also valid terminators.
        const string json = """{"title":"value"   ,"x":"y"}""";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_QuoteAtEndOfInput_TreatedAsStringClose()
    {
        // EOF acts like a structural terminator.
        const string json = "{\"title\":\"value\"";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }

    [Fact]
    public void Sanitize_EmptyString_ReturnsEmpty()
    {
        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(string.Empty);

        Assert.Equal(string.Empty, sanitized);
    }

    [Fact]
    public void Sanitize_NoQuotesInInput_PassesThrough()
    {
        const string json = "[1, 2, 3]";

        string sanitized = GetTranslationUseCase.SanitizeJsonResponse(json);

        Assert.Equal(json, sanitized);
    }
}
