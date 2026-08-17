using System.Net;
using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

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

    [Fact]
    public void BootstrapPolicy_AllowsOnlyConfiguredNetworks()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:BootstrapAllowedNetworks:0"] = "192.168.50.0/24",
                ["Security:BootstrapAllowedNetworks:1"] = "fd12:3456::/48",
            })
            .Build();
        BootstrapAccessPolicy policy = new(configuration);

        Assert.True(policy.IsAllowed(IPAddress.Parse("192.168.50.42")));
        Assert.True(policy.IsAllowed(IPAddress.Parse("::ffff:192.168.50.42")));
        Assert.True(policy.IsAllowed(IPAddress.Parse("fd12:3456::42")));
        Assert.False(policy.IsAllowed(IPAddress.Parse("192.168.51.42")));
        Assert.False(policy.IsAllowed(IPAddress.Loopback));
        Assert.False(policy.IsAllowed(null));
    }
}
