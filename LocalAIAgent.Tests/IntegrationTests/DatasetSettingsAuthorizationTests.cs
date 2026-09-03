using System.Net;
using System.Net.Http.Json;
using LocalAIAgent.API.Infrastructure;

namespace LocalAIAgent.Tests.IntegrationTests;

public sealed class DatasetSettingsAuthorizationTests
{
    [Theory]
    [InlineData("/api/ai-settings")]
    [InlineData("/api/ai-settings/options/1")]
    public async Task MemberCannotChangeDatasetCollection(string path)
    {
        using CustomWebApplicationFactory factory = new() { Role = AuthRoles.Member };
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AddAntiforgeryTokenAsync(client);
        using HttpResponseMessage response = await client.PutAsJsonAsync(path, new
        {
            name = "Model", modelId = "model", endpointUrl = "http://localhost:1234/v1/",
            useResultsForDataset = true,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberCannotCreateDatasetCollectingLlm()
    {
        using CustomWebApplicationFactory factory = new() { Role = AuthRoles.Member };
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AddAntiforgeryTokenAsync(client);
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/ai-settings/options", new
        {
            name = "Model", modelId = "model", endpointUrl = "http://localhost:1234/v1/",
            useResultsForDataset = true,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task AddAntiforgeryTokenAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/auth/csrf", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        string tokenCookie = response.Headers.GetValues("Set-Cookie")
            .Single(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string token = tokenCookie.Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
    }
}
