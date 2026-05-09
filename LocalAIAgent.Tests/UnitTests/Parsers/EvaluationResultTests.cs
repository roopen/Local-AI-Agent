using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Domain;

namespace LocalAIAgent.Tests.UnitTests.Parsers;

public class EvaluationResultTests
{
    [Fact]
    public void Deserialize_PlainJsonArray_ReturnsEntries()
    {
        const string json = """[{"ArticleIndex":0,"Relevancy":"High","Topic":"Tech"}]""";

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        EvaluationResult only = Assert.Single(result);
        Assert.Equal(0, only.ArticleIndex);
        Assert.Equal(Relevancy.High, only.Relevancy);
        Assert.Equal("Tech", only.Topic);
    }

    [Fact]
    public void Deserialize_MultipleEntries_PreservesOrder()
    {
        const string json = """[{"ArticleIndex":0,"Relevancy":"High"},{"ArticleIndex":1,"Relevancy":"Low"}]""";

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].ArticleIndex);
        Assert.Equal(Relevancy.High, result[0].Relevancy);
        Assert.Equal(1, result[1].ArticleIndex);
        Assert.Equal(Relevancy.Low, result[1].Relevancy);
    }

    [Fact]
    public void Deserialize_StripsMarkdownCodeFence()
    {
        const string json = "```json\n[{\"ArticleIndex\":0,\"Relevancy\":\"High\"}]\n```";

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Single(result);
        Assert.Equal(Relevancy.High, result[0].Relevancy);
    }

    [Fact]
    public void Deserialize_StripsThinkBlock()
    {
        const string json = """
            <|think>internal reasoning, may contain stray [ brackets <think|>
            [{"ArticleIndex":0,"Relevancy":"High"}]
            """;

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Single(result);
        Assert.Equal(Relevancy.High, result[0].Relevancy);
    }

    [Fact]
    public void Deserialize_StripsChannelMarker()
    {
        const string json = """
            channel preamble blah blah <channel|>[{"ArticleIndex":0,"Relevancy":"Low"}]
            """;

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Single(result);
        Assert.Equal(Relevancy.Low, result[0].Relevancy);
    }

    [Fact]
    public void Deserialize_StripsBothThinkAndChannel()
    {
        const string json = """
            <|think>noisy thoughts<think|>some prose with <channel|>[{"ArticleIndex":0,"Relevancy":"High","Topic":"News"}]
            """;

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Equal("News", result[0].Topic);
    }

    [Fact]
    public void Deserialize_PicksLastBracketWhenStrayBracketsAppearEarlier()
    {
        // The think block contains a stray '[' that should NOT be the array start.
        // Deserialize uses LastIndexOf to find the actual JSON array start.
        const string json = """
            <|think>I considered [a, b, c] as potential topics<think|>
            [{"ArticleIndex":0,"Relevancy":"High"}]
            """;

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Single(result);
    }

    [Fact]
    public void Deserialize_FixesRelavancyTypo()
    {
        // Some models hallucinate "Relavancy" instead of "Relevancy".
        const string json = """[{"ArticleIndex":0,"Relavancy":"High","Topic":"Tech"}]""";

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Equal(Relevancy.High, result[0].Relevancy);
    }

    [Fact]
    public void Deserialize_NoOpeningBracket_Throws()
    {
        const string json = "no array here at all";

        Assert.Throws<InvalidOperationException>(() => EvaluationResult.Deserialize(json));
    }

    [Fact]
    public void Deserialize_ClosingBracketBeforeOpening_Throws()
    {
        // ']' appears in the input before any '[', so LastIndexOf(']') < LastIndexOf('[')
        // is impossible — but the check guards against missing/inverted close bracket.
        const string json = "stray ] here and then [ but no close after";

        Assert.Throws<InvalidOperationException>(() => EvaluationResult.Deserialize(json));
    }

    [Fact]
    public void Deserialize_TrimsSurroundingWhitespace()
    {
        const string json = """


            [{"ArticleIndex":0,"Relevancy":"High"}]


            """;

        List<EvaluationResult> result = EvaluationResult.Deserialize(json);

        Assert.Single(result);
    }
}
