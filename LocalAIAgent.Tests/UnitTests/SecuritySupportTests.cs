using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace LocalAIAgent.Tests.UnitTests;

public sealed class SecuritySupportTests
{
    [Fact]
    public void InvitationToken_Is256BitsAndHashIsDeterministic()
    {
        string first = InvitationTokens.Create();
        string second = InvitationTokens.Create();

        Assert.Equal(32, WebEncoders.Base64UrlDecode(first).Length);
        Assert.NotEqual(first, second);
        Assert.Equal(64, InvitationTokens.Hash(first).Length);
        Assert.Equal(InvitationTokens.Hash(first), InvitationTokens.Hash(first));
        Assert.NotEqual(InvitationTokens.Hash(first), InvitationTokens.Hash(second));
        Assert.True(InvitationTokens.TryHash(first, out string validatedHash));
        Assert.Equal(InvitationTokens.Hash(first), validatedHash);
        Assert.False(InvitationTokens.TryHash("not-a-valid-invitation", out _));
    }
}
