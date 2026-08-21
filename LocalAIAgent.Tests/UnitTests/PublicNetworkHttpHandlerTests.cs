using System.Net;
using LocalAIAgent.Application.News;

namespace LocalAIAgent.Tests.UnitTests;

public sealed class PublicNetworkHttpHandlerTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("100.100.100.200")]
    [InlineData("127.0.0.1")]
    [InlineData("168.63.129.16")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    public void IsPublicAddress_NonPublicOrSpecialAddress_ReturnsFalse(string value)
    {
        Assert.False(PublicNetworkHttpHandler.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    public void IsPublicAddress_PublicAddress_ReturnsTrue(string value)
    {
        Assert.True(PublicNetworkHttpHandler.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    public async Task HttpClient_PrivateTarget_IsRejectedBeforeConnection(string url)
    {
        using HttpClient client = new(PublicNetworkHttpHandler.Create())
        {
            Timeout = TimeSpan.FromSeconds(2),
        };

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(url, TestContext.Current.CancellationToken));

        Assert.Contains("publicly routable", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpClient_LocalhostName_IsRejectedAfterDnsResolution()
    {
        using HttpClient client = new(PublicNetworkHttpHandler.Create())
        {
            Timeout = TimeSpan.FromSeconds(2),
        };

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://localhost/", TestContext.Current.CancellationToken));

        Assert.Contains("publicly routable", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
