using LocalAIAgent.Domain;
using LocalAIAgent.Application;
using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAIAgent.Application.News.AI
{
    public interface IGetTranslationUseCase
    {
        Task<List<NewsArticle>> TranslateArticleAsync(
            List<NewsArticle> articles,
            string targetLanguage,
            CancellationToken cancellationToken = default,
            int? userPreferencesId = null);
        string GetSystemPrompt(string targetLanguage);
    }

    internal class GetTranslationUseCase(
        IEnumerable<BaseNewsClientSettings> newsClientSettings,
        IArticleTranslationRepository translationRepository,
        ILlmRuntimeManager runtimeManager,
        ILogger<GetTranslationUseCase> logger) : IGetTranslationUseCase
    {
        private const int TranslationBatchSize = 5;
        private const int SingleArticleMaxAttempts = 2;

        // Translation is optional enrichment. Once the model is warm, a stream that produces no
        // text for this long is treated as failed so it cannot hold the news stream open forever.
        internal TimeSpan TranslationInactivityTimeout { get; init; } = TimeSpan.FromSeconds(60);

        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static readonly ChatResponseFormat s_translationResponseFormat =
            ChatResponseFormat.ForJsonSchema<List<TranslationDto>>(
                s_jsonSerializerOptions,
                schemaName: "translations",
                schemaDescription: "An array of translations keyed by their unchanged input index.");

        public async Task<List<NewsArticle>> TranslateArticleAsync(
            List<NewsArticle> articles,
            string targetLanguage,
            CancellationToken cancellationToken = default,
            int? userPreferencesId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Translate any article whose source language differs from the user's target.
            // Article-level language tagging means built-in and custom feeds use the same code path —
            // no host-based registry lookup is needed.
            List<NewsArticle> articlesToTranslate = [.. articles
                .Where(a => !string.IsNullOrEmpty(a.SourceLanguage)
                    && !string.Equals(a.SourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))];

            if (articlesToTranslate.Count is 0) return articles;

            // Check cache first — apply cached translations and filter out already-translated articles
            Dictionary<string, CachedTranslation> cache = await translationRepository
                .GetCachedTranslationsAsync(
                    articlesToTranslate.Select(a => a.Link),
                    targetLanguage,
                    cancellationToken);

            List<NewsArticle> uncachedArticles = [];
            foreach (NewsArticle article in articlesToTranslate)
            {
                if (cache.TryGetValue(article.Link, out CachedTranslation? cached))
                {
                    article.Title = cached.Title;
                    article.Summary = cached.Summary;
                }
                else
                {
                    uncachedArticles.Add(article);
                }
            }

            logger.LogInformation("GetTranslationUseCase: {CachedCount} articles served from cache, {UncachedCount} require translation",
                cache.Count, uncachedArticles.Count);

            if (uncachedArticles.Count is 0) return articles;

            int translatedCount = 0;
            for (int i = 0; i < uncachedArticles.Count; i += TranslationBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<NewsArticle> batch = uncachedArticles.Skip(i).Take(TranslationBatchSize).ToList();
                translatedCount += await TranslateBatchWithFallbackAsync(
                    batch,
                    targetLanguage,
                    userPreferencesId,
                    cancellationToken);
            }

            stopwatch.Stop();
            logger.LogInformation("GetTranslationUseCase: translated {TranslatedCount}/{UncachedCount} articles in {ElapsedMs} ms",
                translatedCount, uncachedArticles.Count, stopwatch.ElapsedMilliseconds);

            return articles;
        }

        private sealed record TranslationDto(
            [property: JsonPropertyName("index")] int Index,
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("summary")] string Summary);

        private sealed record UnindexedTranslationDto(
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("summary")] string Summary);

        private sealed record TranslationAttemptResult(
            int TranslatedCount,
            List<NewsArticle> UnresolvedArticles);

        public string GetSystemPrompt(string targetLanguage)
        {
            string languageName = Languages.GetDisplayName(targetLanguage);
            return $"""
                Translate every news item into {languageName}.
                Preserve facts, names, numbers, dates, quotations, links, and meaning.
                Do not summarize, explain, or add information.
                Return one object per input item in a JSON array.
                Copy each input index unchanged and return only the JSON array.
                """;
        }

        private async Task<int> TranslateBatchWithFallbackAsync(
            List<NewsArticle> batch,
            string targetLanguage,
            int? userPreferencesId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (batch.Count == 0)
                return 0;

            TranslationAttemptResult attempt = await TryTranslateBatchAsync(
                batch,
                targetLanguage,
                userPreferencesId,
                cancellationToken);
            if (attempt.UnresolvedArticles.Count == 0)
                return attempt.TranslatedCount;

            if (batch.Count == 1)
            {
                int translatedCount = attempt.TranslatedCount;
                for (int retry = 1; retry < SingleArticleMaxAttempts; retry++)
                {
                    TranslationAttemptResult retryResult =
                        await TryTranslateBatchAsync(
                            attempt.UnresolvedArticles,
                            targetLanguage,
                            userPreferencesId,
                            cancellationToken);
                    translatedCount += retryResult.TranslatedCount;
                    if (retryResult.UnresolvedArticles.Count == 0)
                        return translatedCount;
                }

                logger.LogWarning("GetTranslationUseCase: failed to translate article {ArticleLink}; leaving original text",
                    batch[0].Link);
                return translatedCount;
            }

            if (attempt.TranslatedCount > 0)
            {
                logger.LogWarning(
                    "GetTranslationUseCase: recovered {TranslatedCount}/{BatchSize} translations; retrying only {MissingCount} missing articles",
                    attempt.TranslatedCount,
                    batch.Count,
                    attempt.UnresolvedArticles.Count);

                return attempt.TranslatedCount
                    + await TranslateBatchWithFallbackAsync(
                        attempt.UnresolvedArticles,
                        targetLanguage,
                        userPreferencesId,
                        cancellationToken);
            }

            int splitIndex = (batch.Count + 1) / 2;
            logger.LogWarning(
                "GetTranslationUseCase: batch of {BatchSize} failed; retrying as batches of {FirstBatchSize} and {SecondBatchSize}",
                batch.Count,
                splitIndex,
                batch.Count - splitIndex);

            List<NewsArticle> firstBatch = batch.GetRange(0, splitIndex);
            List<NewsArticle> secondBatch = batch.GetRange(splitIndex, batch.Count - splitIndex);
            return await TranslateBatchWithFallbackAsync(
                    firstBatch,
                    targetLanguage,
                    userPreferencesId,
                    cancellationToken)
                + await TranslateBatchWithFallbackAsync(
                    secondBatch,
                    targetLanguage,
                    userPreferencesId,
                    cancellationToken);
        }

        private async Task<TranslationAttemptResult> TryTranslateBatchAsync(
            List<NewsArticle> batch,
            string targetLanguage,
            int? userPreferencesId,
            CancellationToken cancellationToken)
        {
            LlmRuntimeSnapshot runtime = runtimeManager.GetRequiredSnapshot(userPreferencesId);
            IChatClient chatClient = runtime.ChatClient;
            AIOptions options = runtime.Options;
            List<(string OriginalTitle, string OriginalSummary)> originals = batch
                .Select(a => (a.Title, a.Summary))
                .ToList();

            var articlesToTranslateForJson = batch.Select((a, index) => new
            {
                index,
                title = a.Title,
                summary = a.Summary
            }).ToList();
            string combinedText = JsonSerializer.Serialize(articlesToTranslateForJson, s_jsonSerializerOptions);

            string systemPrompt = GetSystemPrompt(targetLanguage);
            ChatOptions chatOptions = options.BuildChatOptions(s_translationResponseFormat);
            chatOptions.Temperature = 0;
            chatOptions.FrequencyPenalty = 0;
            chatOptions.PresencePenalty = 0;

            List<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, $"Translate every item. Input JSON:\n{combinedText}"),
            ];

            StringBuilder resultBuilder = new();

            using CancellationTokenSource responseCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            responseCancellation.CancelAfter(TranslationInactivityTimeout);

            try
            {
                await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(
                                    messages,
                                    chatOptions,
                                    responseCancellation.Token)
                                    .ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        resultBuilder.Append(update.Text);
                        responseCancellation.CancelAfter(TranslationInactivityTimeout);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && responseCancellation.IsCancellationRequested)
            {
                logger.LogWarning(
                    "GetTranslationUseCase: translation response timed out after {TimeoutSeconds} seconds of inactivity for batch size {BatchSize}",
                    TranslationInactivityTimeout.TotalSeconds,
                    batch.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "GetTranslationUseCase: translation request failed for batch size {BatchSize}: {Message}",
                    batch.Count,
                    LlmErrorSanitizer.GetSafeMessage(ex));
                throw;
            }

            string result = resultBuilder.ToString();
            List<TranslationDto>? translatedArticles = DeserializeTranslations(result, batch.Count);
            if (translatedArticles is null)
            {
                logger.LogWarning(
                    "GetTranslationUseCase: translation response was not valid JSON in the expected shape. Response: {LlmResponse}",
                    result);
                return new TranslationAttemptResult(0, batch);
            }

            Dictionary<int, TranslationDto> validTranslations = translatedArticles
                .Where(t => t.Index >= 0
                    && t.Index < batch.Count
                    && !string.IsNullOrWhiteSpace(t.Title)
                    && t.Summary is not null)
                .GroupBy(t => t.Index)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());

            List<NewsArticle> translatedBatch = [];
            List<(string OriginalTitle, string OriginalSummary)> translatedOriginals = [];
            List<NewsArticle> unresolvedArticles = [];

            for (int i = 0; i < batch.Count; i++)
            {
                if (!validTranslations.TryGetValue(i, out TranslationDto? translation))
                {
                    unresolvedArticles.Add(batch[i]);
                    continue;
                }

                batch[i].Title = translation.Title;
                batch[i].Summary = translation.Summary;
                translatedBatch.Add(batch[i]);
                translatedOriginals.Add(originals[i]);
            }

            if (unresolvedArticles.Count > 0 || translatedArticles.Count != batch.Count)
            {
                logger.LogWarning(
                    "GetTranslationUseCase: expected {ExpectedCount} unique translations, accepted {AcceptedCount}; {MissingCount} will be retried. Response: {LlmResponse}",
                    batch.Count,
                    translatedBatch.Count,
                    unresolvedArticles.Count,
                    result);
            }

            if (options.UseResultsForDataset && translatedBatch.Count > 0)
            {
                await translationRepository.SaveTranslationsAsync(
                    translatedBatch,
                    translatedOriginals,
                    targetLanguage,
                    cancellationToken);
            }

            return new TranslationAttemptResult(translatedBatch.Count, unresolvedArticles);
        }

        private static List<TranslationDto>? DeserializeTranslations(string result, int expectedCount)
        {
            result = StripResponseDecorations(result);
            string jsonArray = ExtractJsonArray(result);

            // Preserve valid JSON exactly as the provider emitted it. Sanitization is deliberately
            // a fallback because heuristic quote repair can otherwise damage valid formatted JSON.
            if (TryDeserializeTranslations(jsonArray, expectedCount, out List<TranslationDto>? translations))
                return translations;

            string sanitized = SanitizeJsonResponse(jsonArray);
            return sanitized != jsonArray
                && TryDeserializeTranslations(sanitized, expectedCount, out translations)
                    ? translations
                    : null;
        }

        private static bool TryDeserializeTranslations(
            string jsonArray,
            int expectedCount,
            out List<TranslationDto>? translations)
        {
            translations = null;

            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonArray);
                if (document.RootElement.ValueKind == JsonValueKind.Object && expectedCount == 1)
                {
                    string singletonArray = $"[{document.RootElement.GetRawText()}]";
                    return TryDeserializeTranslations(singletonArray, expectedCount, out translations);
                }

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return false;

                JsonElement[] elements = [.. document.RootElement.EnumerateArray()];
                bool allIndexed = elements.All(HasIndexProperty);
                if (allIndexed)
                {
                    translations = JsonSerializer.Deserialize<List<TranslationDto>>(
                        jsonArray,
                        s_jsonSerializerOptions);
                    return translations is not null;
                }

                // Models occasionally omit every index while preserving array order. Accept that
                // shape only when the complete batch is present, so partial responses can never be
                // assigned to the wrong articles.
                bool noneIndexed = elements.All(element => !HasIndexProperty(element));
                if (!noneIndexed || elements.Length != expectedCount)
                    return false;

                List<UnindexedTranslationDto>? unindexed =
                    JsonSerializer.Deserialize<List<UnindexedTranslationDto>>(
                        jsonArray,
                        s_jsonSerializerOptions);
                if (unindexed is null)
                    return false;

                translations = unindexed
                    .Select((translation, index) =>
                        new TranslationDto(index, translation.Title, translation.Summary))
                    .ToList();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool HasIndexProperty(JsonElement element) =>
            element.ValueKind == JsonValueKind.Object
            && element.EnumerateObject().Any(property =>
                property.Name.Equals("index", StringComparison.OrdinalIgnoreCase));

        private static string StripResponseDecorations(string result)
        {
            const string channelMarker = "<channel|>";
            int markerIndex = result.LastIndexOf(channelMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
                result = result[(markerIndex + channelMarker.Length)..];

            return result
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();
        }

        private static string ExtractJsonArray(string result)
        {
            int arrayStart = result.IndexOf('[');
            int arrayEnd = result.LastIndexOf(']');
            return arrayStart >= 0 && arrayEnd >= arrayStart
                ? result[arrayStart..(arrayEnd + 1)]
                : result;
        }

        /// <summary>
        /// Returns true when <paramref name="articleSource"/> belongs to the same site as
        /// <paramref name="settingsHost"/>, including sibling subdomains.
        /// e.g. "news.ltn.com.tw" matches settings host "www.ltn.com.tw" via shared parent "ltn.com.tw".
        /// </summary>
        internal static bool MatchesHost(string articleSource, string settingsHost)
        {
            if (articleSource.Equals(settingsHost, StringComparison.OrdinalIgnoreCase))
                return true;

            // Article is a subdomain of the settings host (e.g. "foo.example.com" under "example.com")
            if (articleSource.EndsWith("." + settingsHost, StringComparison.OrdinalIgnoreCase))
                return true;

            // Strip one subdomain level from the settings host and retry, so that sibling subdomains
            // ("news.ltn.com.tw", "ent.ltn.com.tw") all match a configured host like "www.ltn.com.tw"
            // by sharing the parent domain "ltn.com.tw".
            int dot = settingsHost.IndexOf('.');
            if (dot >= 0)
            {
                string parent = settingsHost[(dot + 1)..];
                if (articleSource.Equals(parent, StringComparison.OrdinalIgnoreCase)
                    || articleSource.EndsWith("." + parent, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // Repairs common invalid-JSON patterns emitted by LLMs before deserialization.
        internal static string SanitizeJsonResponse(string json)
        {
            // \' is not a valid JSON escape sequence
            json = json.Replace("\\'", "'");

            // Fix unescaped double quotes inside string values using a state machine.
            // Heuristic: a '"' that is followed (ignoring whitespace) by ':', ',', '}', ']', or EOF
            // is treated as a string delimiter; anything else is an embedded quote and gets escaped.
            StringBuilder sb = new System.Text.StringBuilder(json.Length);
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped)
                {
                    sb.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    sb.Append(c);
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    if (!inString)
                    {
                        inString = true;
                        sb.Append(c);
                        continue;
                    }

                    // Look ahead past whitespace to decide if this closes the string.
                    int j = i + 1;
                    while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                    char next = j < json.Length ? json[j] : '\0';

                    if (next is ':' or ',' or '}' or ']' or '\0')
                    {
                        inString = false;
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append("\\\"");
                    }
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
