using LocalAIAgent.Domain;

namespace LocalAIAgent.Tests.UnitTests;

public class UserPreferencesTests
{
    private static UserPreferences MakePrefs(List<string> interests, List<string> dislikes, string prompt = "Be helpful.") =>
        new() { Prompt = prompt, Interests = interests, Dislikes = dislikes };

    [Fact]
    public void BuildSystemPrompt_EmbedsInterestsAndDislikes()
    {
        UserPreferences prefs = MakePrefs(["AI", "Space"], ["Sports"]);

        string output = prefs.BuildSystemPrompt();

        Assert.Contains("AI, Space", output);
        Assert.Contains("Sports", output);
    }

    [Fact]
    public void BuildSystemPrompt_EmbedsUserPrompt()
    {
        UserPreferences prefs = MakePrefs(["AI"], [], prompt: "Focus on local news.");

        string output = prefs.BuildSystemPrompt();

        Assert.Contains("Focus on local news.", output);
    }

    [Fact]
    public void BuildSystemPrompt_EmptyInterests_ProducesEmptyLikesSection()
    {
        UserPreferences prefs = MakePrefs([], ["Sports"]);

        string output = prefs.BuildSystemPrompt();

        Assert.Contains("User's likes: \n\n", output);
        Assert.Contains("Sports", output);
    }

    [Fact]
    public void BuildSystemPrompt_EmptyDislikes_ProducesEmptyDislikesSection()
    {
        UserPreferences prefs = MakePrefs(["AI"], []);

        string output = prefs.BuildSystemPrompt();

        Assert.Contains("User's dislikes: \n\n", output);
        Assert.Contains("AI", output);
    }

    [Fact]
    public void BuildSystemPrompt_AlwaysIncludesJsonSchemaContract()
    {
        UserPreferences prefs = MakePrefs(["AI"], []);

        string output = prefs.BuildSystemPrompt();

        // The contract the prompt enforces is what makes the model output parseable —
        // if any of these break, EvaluationResult.Deserialize will start failing.
        Assert.Contains("ArticleIndex", output);
        Assert.Contains("Relevancy", output);
        Assert.Contains("Topic", output);
        Assert.Contains("High, Low", output);
    }
}
