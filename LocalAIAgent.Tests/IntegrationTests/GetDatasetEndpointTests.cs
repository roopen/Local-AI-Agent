using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace LocalAIAgent.Tests.IntegrationTests;

public class GetDatasetEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetDataset_WithNoData_ReturnsOkWithEmptyZip()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/News/Dataset");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
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

            User user = new()
            {
                Fido2Id = [1, 2, 3],
                Username = "dataset-test-user",
                Preferences = new UserPreferences
                {
                    Prompt = "You are a helpful news evaluator.",
                    Interests = ["Technology"],
                    Dislikes = [],
                    EvaluationEntries =
                    [
                        new NewsEvaluationEntry
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
                        new NewsEvaluationEntry
                        {
                            ArticleTitle = "Stock Market Update",
                            ArticleSummary = "Markets close higher today.",
                            ArticleLink = "https://example.com/stocks",
                            ArticleSource = "example.com",
                            ArticleTopic = "Finance",
                            Relevancy = "Low",
                            ModelUsed = "test-model",
                        },
                        new NewsEvaluationEntry
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
            await db.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/News/Dataset");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("dataset.zip", response.Content.Headers.ContentDisposition?.FileName);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);

        ZipArchiveEntry? trainEntry = archive.GetEntry("training_dataset.jsonl");
        ZipArchiveEntry? evalEntry = archive.GetEntry("evaluation_dataset.jsonl");

        Assert.NotNull(trainEntry);
        Assert.NotNull(evalEntry);

        using StreamReader trainReader = new(trainEntry.Open());
        string trainContent = await trainReader.ReadToEndAsync();

        using StreamReader evalReader = new(evalEntry.Open());
        string evalContent = await evalReader.ReadToEndAsync();

        // Both files together must have at least one entry total
        int totalLines = trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                       + evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(totalLines > 0, "Expected at least one JSONL entry across train and eval files.");

        // Each non-empty line must be valid JSON with a "messages" array
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
                db.ArticleTranslations.Add(new ArticleTranslation
                {
                    ArticleLink = $"https://example.com/article-{i}-translation",
                    OriginalTitle = $"Original Title {i}",
                    OriginalSummary = $"Original summary for article {i}.",
                    TranslatedTitle = $"Título {i}",
                    TranslatedSummary = $"Resumen del artículo {i}.",
                    TargetLanguage = "Spanish",
                });
            }
            await db.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/News/Dataset");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);

        ZipArchiveEntry? trainEntry = archive.GetEntry("training_dataset.jsonl");
        ZipArchiveEntry? evalEntry = archive.GetEntry("evaluation_dataset.jsonl");

        Assert.NotNull(trainEntry);
        Assert.NotNull(evalEntry);

        using StreamReader trainReader = new(trainEntry.Open());
        string trainContent = await trainReader.ReadToEndAsync();

        using StreamReader evalReader = new(evalEntry.Open());
        string evalContent = await evalReader.ReadToEndAsync();

        int totalLines = trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length
                       + evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(totalLines > 0, "Expected translation entries in the dataset.");
    }
}
