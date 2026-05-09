using LocalAIAgent.Application;

namespace LocalAIAgent.Tests.UnitTests;

public class LanguagesTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ja")]
    [InlineData("zh-TW")]
    [InlineData("pt-BR")]
    [InlineData("EN")]   // case-insensitive
    public void IsSupported_AcceptsAnyValidIsoCulture(string code)
    {
        Assert.True(Languages.IsSupported(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("zz-totally-fake")]
    [InlineData("not-a-language")]
    public void IsSupported_RejectsInvalidOrEmpty(string code)
    {
        Assert.False(Languages.IsSupported(code));
    }

    [Fact]
    public void GetDisplayName_ReturnsEnglishNameForKnownCode()
    {
        Assert.Equal("English", Languages.GetDisplayName("en"));
        Assert.Equal("Japanese", Languages.GetDisplayName("ja"));
    }

    [Fact]
    public void GetDisplayName_FallsBackToCodeForUnknown()
    {
        Assert.Equal("zz-fake", Languages.GetDisplayName("zz-fake"));
    }

    [Fact]
    public void GetAllSupported_ContainsCommonLanguagesAndZhTw()
    {
        IReadOnlyList<(string Code, string Name)> all = Languages.GetAllSupported();

        // Sanity: must have at least the common ones we already declare on built-in feeds.
        Assert.Contains(all, l => l.Code == "en");
        Assert.Contains(all, l => l.Code == "ja");
        Assert.Contains(all, l => l.Code == "fr");
        Assert.Contains(all, l => l.Code == "zh-TW");

        // Sorted alphabetically by display name (case-insensitive).
        for (int i = 1; i < all.Count; i++)
        {
            int cmp = string.Compare(all[i - 1].Name, all[i].Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(cmp <= 0, $"List not sorted at {i}: '{all[i - 1].Name}' followed by '{all[i].Name}'");
        }
    }
}
