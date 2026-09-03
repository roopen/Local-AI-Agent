using LocalAIAgent.API.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InfraModels = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.IntegrationTests;

public sealed class GetDatasetEndpointTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();
    private readonly HttpClient _httpClient;

    public GetDatasetEndpointTests() => _httpClient = _factory.CreateClient();

    public void Dispose()
    {
        _httpClient.Dispose();
        _factory.Dispose();
    }

    private async Task<(int StatusCode, byte[] Bytes, string? ContentType, string? FileName)> GetDatasetAsync(string? modelId = null)
    {
        string url = modelId is null ? "/api/News/Dataset" : $"/api/News/Dataset?modelId={Uri.EscapeDataString(modelId)}";
        using HttpResponseMessage response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        string? fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        string? contentType = response.Content.Headers.ContentType?.MediaType;
        return ((int)response.StatusCode, bytes, contentType, fileName);
    }

    private static InfraModels.NewsEvaluationEntry EvaluationEntry(string title, string link, string modelUsed, bool useInDataset = true) => new()
    {
        ArticleTitle = title,
        ArticleSummary = $"Summary for {title}.",
        ArticleLink = link,
        ArticleSource = "example.com",
        ArticleTopic = "Technology",
        Relevancy = "High",
        ModelUsed = modelUsed,
        UseInDataset = useInDataset,
    };

    private static async Task<string> ReadAllDatasetContentAsync(ZipArchive archive)
    {
        StringBuilder content = new();
        string[] entryNames = ["training_dataset.jsonl", "evaluation_dataset.jsonl"];
        foreach (string entryName in entryNames)
        {
            ZipArchiveEntry? entry = archive.GetEntry(entryName);
            Assert.NotNull(entry);

            using StreamReader reader = new(entry.Open());
            content.AppendLine(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        return content.ToString();
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
        Assert.True(string.IsNullOrWhiteSpace(await ReadAllDatasetContentAsync(archive)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task GetDataset_WithEvaluationEntriesAndNoModelFilter_ReturnsZipWithJsonlEntries(string? modelId)
    {
        // Arrange — seed a user with preferences and evaluation entries
        using (IServiceScope scope = _factory.Services.CreateScope())
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
                            UseInDataset = true,
                        },
                        new InfraModels.NewsEvaluationEntry
                        {
                            ArticleTitle = "Stock Market Update",
                            ArticleSummary = "Markets close higher today.",
                            ArticleLink = "https://example.com/stocks",
                            ArticleSource = "example.com",
                            ArticleTopic = "Finance",
                            Relevancy = "Low",
                            ModelUsed = "other-model",
                            UseInDataset = true,
                        },
                        new InfraModels.NewsEvaluationEntry
                        {
                            ArticleTitle = ".NET 10 Released",
                            ArticleSummary = "Microsoft releases .NET 10.",
                            ArticleLink = "https://example.com/dotnet10",
                            ArticleSource = "example.com",
                            ArticleTopic = "Technology",
                            Relevancy = "High",
                            ModelUsed = "Unknown",
                            UseInDataset = true,
                        },
                        EvaluationEntry("Excluded cached article", "https://example.com/excluded", "test-model", useInDataset: false),
                    ]
                }
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        (int statusCode, byte[] bytes, string? contentType, string? fileName) = await GetDatasetAsync(modelId);

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

        string allContent = trainContent + evalContent;
        Assert.Contains("AI Breakthrough", allContent);
        Assert.Contains("Stock Market Update", allContent);
        Assert.Contains(".NET 10 Released", allContent);
        Assert.DoesNotContain("Excluded cached article", allContent);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task GetDataset_WithTranslationsAndNoModelFilter_IncludesTranslationEntries(string? modelId)
    {
        // Arrange — seed translations only (no evaluation entries)
        using (IServiceScope scope = _factory.Services.CreateScope())
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
        (int statusCode, byte[] bytes, _, _) = await GetDatasetAsync(modelId);

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

        bool foundTranslationEntry = false;
        foreach (string line in trainContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(evalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)))
        {
            using JsonDocument entry = JsonDocument.Parse(line);
            JsonElement messages = entry.RootElement.GetProperty("messages");
            string systemPrompt = messages[0].GetProperty("content").GetString() ?? string.Empty;
            if (!systemPrompt.StartsWith("Translate every news item into", StringComparison.Ordinal))
                continue;

            foundTranslationEntry = true;
            string assistantContent = messages[2].GetProperty("content").GetString()!;
            using JsonDocument translations = JsonDocument.Parse(assistantContent);
            Assert.Equal(JsonValueKind.Array, translations.RootElement.ValueKind);
            Assert.All(translations.RootElement.EnumerateArray(), translation =>
            {
                Assert.True(translation.TryGetProperty("index", out _));
                Assert.True(translation.TryGetProperty("title", out _));
                Assert.True(translation.TryGetProperty("summary", out _));
            });
        }

        Assert.True(foundTranslationEntry, "Expected an indexed-array translation sample.");
    }

    [Theory]
    [InlineData("model-a", "model-a")]
    [InlineData("  model-a  ", "model-a")]
    [InlineData("publisher/model-a:4b+q8", "publisher/model-a:4b+q8")]
    public async Task GetDataset_WithModelIdFilter_ReturnsOnlyEntriesFromThatModel(string modelId, string modelUsed)
    {
        // Arrange — seed entries collected by two different models plus a translation
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();

            InfraModels.User user = new()
            {
                Fido2Id = [1, 2, 3],
                Username = "dataset-model-filter-user",
                Preferences = new InfraModels.UserPreferences
                {
                    Prompt = "You are a helpful news evaluator.",
                    Interests = ["Technology"],
                    Dislikes = [],
                    EvaluationEntries =
                    [
                        EvaluationEntry("Alpha model report", "https://example.com/alpha-1", modelUsed),
                        EvaluationEntry("Beta market watch", "https://example.com/beta-1", "model-b"),
                        EvaluationEntry("Gamma tech review", "https://example.com/gamma-1", modelUsed),
                        EvaluationEntry("Unknown model report", "https://example.com/unknown-1", "Unknown"),
                        EvaluationEntry("Excluded cached article", "https://example.com/excluded", modelUsed, useInDataset: false),
                    ]
                }
            };

            db.Users.Add(user);
            db.ArticleTranslations.Add(new InfraModels.ArticleTranslation
            {
                ArticleLink = "https://example.com/translated-article",
                OriginalTitle = "Original translated title",
                OriginalSummary = "Original translated summary.",
                TranslatedTitle = "Título traducido",
                TranslatedSummary = "Resumen traducido.",
                TargetLanguage = "Spanish",
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        (int statusCode, byte[] bytes, _, _) = await GetDatasetAsync(modelId);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, statusCode);

        using ZipArchive archive = new(new MemoryStream(bytes), ZipArchiveMode.Read);
        string allContent = await ReadAllDatasetContentAsync(archive);

        Assert.Contains("Alpha model report", allContent);
        Assert.Contains("Gamma tech review", allContent);
        Assert.DoesNotContain("Beta market watch", allContent);
        Assert.DoesNotContain("Unknown model report", allContent);
        Assert.DoesNotContain("Excluded cached article", allContent);
        Assert.DoesNotContain("Translate every news item into", allContent);
    }

    [Theory]
    [InlineData("unknown-model")]
    [InlineData("MODEL-A")]
    [InlineData("model")]
    public async Task GetDataset_WithNonMatchingModelId_ReturnsNotFound(string modelId)
    {
        // Arrange — seed entries for a single model only
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();

            InfraModels.User user = new()
            {
                Fido2Id = [1, 2, 3],
                Username = "dataset-model-404-user",
                Preferences = new InfraModels.UserPreferences
                {
                    Prompt = "You are a helpful news evaluator.",
                    Interests = ["Technology"],
                    Dislikes = [],
                    EvaluationEntries =
                    [
                        EvaluationEntry("Alpha model report", "https://example.com/alpha-2", "model-a"),
                    ]
                }
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        using HttpResponseMessage response = await _httpClient.GetAsync(
            $"/api/News/Dataset?modelId={Uri.EscapeDataString(modelId)}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal($"No dataset entries found for model '{modelId}'.",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DatasetModelsListsDistinctEligibleHistoricalModels()
    {
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            UserContext db = scope.ServiceProvider.GetRequiredService<UserContext>();
            db.Users.Add(new InfraModels.User
            {
                Fido2Id = [1], Username = "historical-models",
                Preferences = new InfraModels.UserPreferences
                {
                    EvaluationEntries =
                    [
                        EvaluationEntry("Beta", "https://example.com/b", "retired-model"),
                        EvaluationEntry("Alpha", "https://example.com/a", "alpha-model"),
                        EvaluationEntry("Alpha again", "https://example.com/a2", "alpha-model"),
                        EvaluationEntry("Excluded", "https://example.com/x", "excluded-model", useInDataset: false),
                    ],
                },
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        string[]? models = await _httpClient.GetFromJsonAsync<string[]>(
            "/api/News/Dataset/Models", TestContext.Current.CancellationToken);
        Assert.NotNull(models);
        Assert.Equal(["alpha-model", "retired-model"], models);
        using HttpResponseMessage excluded = await _httpClient.GetAsync(
            "/api/News/Dataset?modelId=excluded-model", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, excluded.StatusCode);
    }

    [Fact]
    public async Task DatasetModelsWithNoDataReturnsEmptyList()
    {
        string[]? models = await _httpClient.GetFromJsonAsync<string[]>(
            "/api/News/Dataset/Models", TestContext.Current.CancellationToken);
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Theory]
    [InlineData(AuthRoles.Member, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task DatasetEndpointsRequireOwner(string? role, HttpStatusCode expected)
    {
        using CustomWebApplicationFactory restrictedFactory = new() { Role = role };
        using HttpClient client = restrictedFactory.CreateClient();
        foreach (string path in new[] { "/api/News/Dataset", "/api/News/Dataset/Models" })
        {
            using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
        }
    }
}
