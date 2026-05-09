using LocalAIAgent.Application.News.AI;

namespace LocalAIAgent.Tests.UnitTests.Parsers;

public class TopicNormalizationTests
{
    // -------- NormalizeLabel --------

    [Fact]
    public void NormalizeLabel_PlainAsciiLabel_Unchanged()
    {
        Assert.Equal("Technology", EvaluateNewsUseCase.NormalizeLabel("Technology"));
    }

    [Fact]
    public void NormalizeLabel_TrimsSurroundingWhitespace()
    {
        Assert.Equal("Finance", EvaluateNewsUseCase.NormalizeLabel("   Finance   "));
    }

    [Fact]
    public void NormalizeLabel_StripsLeadingDigits()
    {
        // The model sometimes prefixes labels with numbers like "1. Tech".
        Assert.Equal("Tech", EvaluateNewsUseCase.NormalizeLabel("123Tech"));
    }

    [Fact]
    public void NormalizeLabel_StripsLeadingPunctuation()
    {
        Assert.Equal("Geopolitics", EvaluateNewsUseCase.NormalizeLabel("- Geopolitics"));
    }

    [Fact]
    public void NormalizeLabel_AllNonLetterInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, EvaluateNewsUseCase.NormalizeLabel("---"));
    }

    // -------- IsCompoundLabel --------

    [Fact]
    public void IsCompoundLabel_NoSlash_ReturnsFalse()
    {
        HashSet<string> existing = ["Technology"];

        Assert.False(EvaluateNewsUseCase.IsCompoundLabel("Technology", existing));
    }

    [Fact]
    public void IsCompoundLabel_SlashAndOnePartIsKnown_ReturnsTrue()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "Technology" };

        Assert.True(EvaluateNewsUseCase.IsCompoundLabel("Technology/Finance", existing));
    }

    [Fact]
    public void IsCompoundLabel_SlashButNoPartIsKnown_ReturnsFalse()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "Politics" };

        Assert.False(EvaluateNewsUseCase.IsCompoundLabel("Technology/Finance", existing));
    }

    [Fact]
    public void IsCompoundLabel_TolerantOfWhitespaceAroundSlash()
    {
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase) { "Finance" };

        Assert.True(EvaluateNewsUseCase.IsCompoundLabel("Technology / Finance", existing));
    }

    // -------- FormatKnownTopics --------

    [Fact]
    public void FormatKnownTopics_EmptySet_ReturnsSingleLineFallbackRule()
    {
        HashSet<string> topics = [];

        string output = EvaluateNewsUseCase.FormatKnownTopics(topics);

        Assert.Contains("Topic Rule", output);
        Assert.DoesNotContain("[Known Topics]", output);
    }

    [Fact]
    public void FormatKnownTopics_NonEmpty_IncludesKnownTopicsSection()
    {
        HashSet<string> topics = new(StringComparer.OrdinalIgnoreCase) { "Technology" };

        string output = EvaluateNewsUseCase.FormatKnownTopics(topics);

        Assert.Contains("[Known Topics]", output);
        Assert.Contains("Technology", output);
    }

    [Fact]
    public void FormatKnownTopics_MultipleTopics_AreSortedAlphabetically()
    {
        HashSet<string> topics = new(StringComparer.OrdinalIgnoreCase) { "Zebra", "Apple", "Mango" };

        string output = EvaluateNewsUseCase.FormatKnownTopics(topics);

        // Look only at the rendered known-topics line; the rule preamble mentions
        // other words like "Finance" that would distort whole-output IndexOf lookups.
        Assert.Contains("- Apple, Mango, Zebra", output);
    }
}
