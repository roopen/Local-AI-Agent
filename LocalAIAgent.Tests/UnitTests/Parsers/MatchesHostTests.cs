using LocalAIAgent.Application.News.AI;

namespace LocalAIAgent.Tests.UnitTests.Parsers;

public class MatchesHostTests
{
    [Fact]
    public void ExactMatch_ReturnsTrue()
    {
        Assert.True(GetTranslationUseCase.MatchesHost("news.example.com", "news.example.com"));
    }

    [Fact]
    public void ExactMatch_IsCaseInsensitive()
    {
        Assert.True(GetTranslationUseCase.MatchesHost("NEWS.example.com", "news.EXAMPLE.com"));
    }

    [Fact]
    public void ArticleIsSubdomainOfSettingsHost_ReturnsTrue()
    {
        // foo.example.com is under example.com
        Assert.True(GetTranslationUseCase.MatchesHost("foo.example.com", "example.com"));
    }

    [Fact]
    public void SiblingSubdomainsShareSameParent_ReturnsTrue()
    {
        // The doc-comment example: both news.ltn.com.tw and www.ltn.com.tw share
        // parent "ltn.com.tw" once one subdomain level is stripped.
        Assert.True(GetTranslationUseCase.MatchesHost("news.ltn.com.tw", "www.ltn.com.tw"));
        Assert.True(GetTranslationUseCase.MatchesHost("ent.ltn.com.tw", "www.ltn.com.tw"));
    }

    [Fact]
    public void DifferentDomainsWithSharedTld_ReturnsFalse()
    {
        Assert.False(GetTranslationUseCase.MatchesHost("bbc.co.uk", "cnn.com"));
    }

    [Fact]
    public void SubstringMatch_DoesNotMatch_For3PartSettingsHost()
    {
        // With a 3-part settings host the parent strip yields a real domain
        // ("example.com"), so a substring like "myexample.com" correctly fails to match.
        // (The 2-part host case is over-permissive — see KnownOverPermissive test below.)
        Assert.False(GetTranslationUseCase.MatchesHost("myexample.com", "www.example.com"));
    }

    [Fact]
    public void TwoPartHosts_WithDifferentSecondLevel_ReturnTrue_KnownOverPermissive()
    {
        // Known weakness: when settingsHost is a 2-part domain ("foo.com"), stripping
        // one level yields the public TLD ("com"), so any other ".com" host matches.
        // This documents current behavior; the proper fix needs a public-suffix list.
        Assert.True(GetTranslationUseCase.MatchesHost("totally-unrelated.com", "example.com"));
    }
}
