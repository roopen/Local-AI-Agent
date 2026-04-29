using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.Tests.Generated;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.IntegrationTests;

public class GetDatasetEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _httpClient = factory.CreateClient();

    private UserClient CreateClient() => new(string.Empty, _httpClient);

    private async Task<(int StatusCode, byte[] Bytes, string? ContentType, string? FileName)> GetDatasetAsync()
    {
        UserClient client = CreateClient();
        SwaggerResponse response = await client.DatasetAsync();
        byte[] bytes = await _httpClient.GetByteArrayAsync("/api/News/Dataset");
        response.Headers.TryGetValue("Content-Disposition", out IEnumerable<string>? cd);
        string? fileName = cd?.FirstOrDefault()?.Split("filename=").ElementAtOrDefault(1)?.Trim('"');
        response.Headers.TryGetValue("Content-Type", out IEnumerable<string>? ct);
        return (response.StatusCode, bytes, ct?.FirstOrDefault(), fileName);
    }

    [Fact]
    public async Task GetDataset_WithNoData_ReturnsOkWithEmptyZip()
    {
        (int statusCode, byte[] bytes, string? contentType, _) = await GetDatasetAsync();

        Assert.Equal((int)HttpStatusCode.OK, statusCode);
        Assert.Equal("application/zip", contentType);

        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Contains(archive.Entries, e => e.Name == "training_dataset.jsonl");
        Assert.Contains(archive.Entries, e => e.Name == "evaluation_dataset.jsonl");
    }

    [Fact]
    public async Task GetDataset_WithEvaluationEntries_ReturnsZipWithJsonlEntries()
    {
        // Arrange — seed a user with preferences and evaluation entries
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();

            InfraModels.User user = new()
            {
                Fido2Id = [1, 2, 3],
                Username = "dataset-test-user",
                Preferences = new InfraModels.UserPreferences
                {
                    Prompt = "You are a helpful news evaluator.",
                    Interests = ["Technology"],
                    Dislikes = [],
                    EvaluationEntries =
                    [
                        new InfraModels.NewsEvaluationEntry
                        {
                            ArticleTitle = "AI Breakthrough",
                            ArticleSummary = "Scientists develop new AI model.",
                            ArticleLink = "https://example.com/ai-breakthrough",
                            ArticleSource = "example.com",
                            ArticleTopic = "Technology",
                            Relevancy = "High",
                            Reasoning = "Relevant to tech interests.",
                            ModelUsed = "test-model",
                        },
                        new InfraModels.NewsEvaluationEntry
                        {
                            ArticleTitle = "Stock Market Update",
                            ArticleSummary = "Markets close higher today.",
                            ArticleLink = "https://example.com/stocks",
                            ArticleSource = "example.com",
                            ArticleTopic = "Finance",
                            Relevancy = "Low",
                            ModelUsed = "test-model",
                        },
                        new InfraModels.NewsEvaluationEntry
                        {
                            ArticleTitle = ".NET 10 Released",
                            ArticleSummary = "Microsoft releases .NET 10.",
                            ArticleLink = "https://example.com/dotnet10",
                            ArticleSource = "example.com",
                            ArticleTopic = "Technology",
                            Relevancy = "High",
                            ModelUsed = "test-model",
                        },
                    ]
                }
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        (int statusCode, byte[] bytes, string? contentType, string? fileName) = await GetDatasetAsync();

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, statusCode);
        Assert.Equal("application/zip", contentType);
        Assert.Contains("dataset.zip", fileName ?? string.Empty);

        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);

        ZipArchiveEntry? trainEntry = archive.GetEntry("training_dataset.jsonl");
        ZipArchiveEntry? evalEntry = archive.GetEntry("evaluation_dataset.jsonl");

        Assert.NotNull(trainEntry);
        Assert.NotNull(evalEntry);

        using StreamReader trainReader = new(trainEntry.Open());
        string trainContent = await trainReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        using StreamReader evalReader = new(evalEntry.Open());
        string evalContent = await evalReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        int totalLines = trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                       + evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(totalLines > 0, "Expected at least one JSONL entry across train and eval files.");

        foreach (string line in trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)))
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            Assert.True(doc.RootElement.TryGetProperty("messages", out JsonElement messages));
            Assert.Equal(JsonValueKind.Array, messages.ValueKind);
            Assert.True(messages.GetArrayLength() == 3, "Each entry must have system, user, and assistant messages.");
        }
    }

    [Fact]
    public async Task GetDataset_WithTranslations_IncludesTranslationEntries()
    {
        // Arrange — seed translations only (no evaluation entries)
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();

            for (int i = 1; i <= 4; i++)
            {
                db.ArticleTranslations.Add(new InfraModels.ArticleTranslation
                {
                    ArticleLink = $"https://example.com/article-{i}-translation",
                    OriginalTitle = $"Original Title {i}",
                    OriginalSummary = $"Original summary for article {i}.",
                    TranslatedTitle = $"Título {i}",
                    TranslatedSummary = $"Resumen del artículo {i}.",
                    TargetLanguage = "Spanish",
                });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        (int statusCode, byte[] bytes, _, _) = await GetDatasetAsync();

        Assert.Equal((int)HttpStatusCode.OK, statusCode);

        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);

        ZipArchiveEntry? trainEntry = archive.GetEntry("training_dataset.jsonl");
        ZipArchiveEntry? evalEntry = archive.GetEntry("evaluation_dataset.jsonl");

        Assert.NotNull(trainEntry);
        Assert.NotNull(evalEntry);

        using StreamReader trainReader = new(trainEntry.Open());
        string trainContent = await trainReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        using StreamReader evalReader = new(evalEntry.Open());
        string evalContent = await evalReader.ReadToEndAsync(TestContext.Current.CancellationToken);

        int totalLines = trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                       + evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(totalLines > 0, "Expected translation entries in the dataset.");
    }
}
