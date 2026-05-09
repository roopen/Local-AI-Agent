using LocalAIAgent.Domain;
using LocalAIAgent.Application;
using LocalAIAgent.Application.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalAIAgent.Application.News.AI
{
    public interface IGetTranslationUseCase
    {
        Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage);
        string GetSystemPrompt(string targetLanguage);
    }

    internal class GetTranslationUseCase(
        IEnumerable<BaseNewsClientSettings> newsClientSettings,
        IArticleTranslationRepository translationRepository,
        IChatClient chatClient,
        AIOptions options,
        ILogger<GetTranslationUseCase> logger) : IGetTranslationUseCase
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly JsonSerializerOptions s_jsonDeserializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            // Translate any source whose published language differs from the user's target.
            List<BaseNewsClientSettings> sourcesToTranslate = newsClientSettings
                .Where(s => !string.Equals(s.Language, targetLanguage, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sourcesToTranslate.Count is 0) return articles;

            List<NewsArticle> articlesToTranslate = [];

            foreach (BaseNewsClientSettings source in sourcesToTranslate)
            {
                string sourceName = source.ClientName.Replace("Client", null).ToLowerInvariant();

                // Filter articles that belong to the current source and require translation
                articlesToTranslate.AddRange(articles.Where(a => MatchesHost(a.Source, source.Host)
                    || source.AdditionalHosts.Any(h => MatchesHost(a.Source, h))
                    || a.Source.Contains(sourceName)));
            }

            if (articlesToTranslate.Count is 0) return articles;

            // Check cache first — apply cached translations and filter out already-translated articles
            Dictionary<string, CachedTranslation> cache = await translationRepository
                .GetCachedTranslationsAsync(articlesToTranslate.Select(a => a.Link), targetLanguage);

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

            // Process batches of 3 in parallel
            List<Task> batchTasks = [];
            for (int i = 0; i < uncachedArticles.Count; i += 3)
            {
                List<NewsArticle> batch = uncachedArticles.Skip(i).Take(3).ToList();
                batchTasks.Add(TranslateBatchAsync(batch, targetLanguage));
            }
            await Task.WhenAll(batchTasks);
            stopwatch.Stop();
            logger.LogInformation("GetTranslationUseCase: translated {UncachedCount} articles in {ElapsedMs} ms",
                uncachedArticles.Count, stopwatch.ElapsedMilliseconds);

            return articles;
        }

        private record TranslationDto(string Title, string Summary);

        public string GetSystemPrompt(string targetLanguage)
        {
            // Accept either an ISO code ("en") or a name ("English"); the LLM gets the name.
            string languageName = Languages.GetDisplayName(targetLanguage);
            return $@"
                <|think|>
                ## Role
                Translate news JSON objects into {languageName}.

                ## Critical Logic (<|channel>thought)
                For each article:
                1. Identify Source Language (e.g., Traditional Chinese).
                2. List 2-3 'Anchor Terms' (e.g., OPEC+, AFP, technical nouns) and their {languageName} equivalents.
                3. Explicitly set internal state to {languageName} mode.
                *Do NOT write full draft sentences here.*

                ## Output Rules
                - Provide ONLY the JSON array after the <channel|> tag.
                - Translate 'title' and 'summary' only.
                - Strict JSON: No markdown, no trailing commas, start with '['.

                [EXAMPLE]
                User: [{{""title"": ""OPEC+：能源設施修復費時"", ""summary"": ""法新社報導...""}}]
                Model:
                <|channel>thought
                - Art 0: Traditional Chinese.
                - Anchors: OPEC+ (OPEC+), 法新社 (AFP), 修復 (Repair).
                - Mode: {languageName}.
                <channel|>
                [{{""title"": ""OPEC+: Energy Facility Repairs Are Time-Consuming"", ""summary"": ""AFP reports...""}}]
                [END EXAMPLE]
                <|turn>";
        }

        private async Task TranslateBatchAsync(List<NewsArticle> batch, string targetLanguage, int attempt = 0)
        {
            // Capture originals before they are overwritten
            List<(string OriginalTitle, string OriginalSummary)> originals = batch
                .Select(a => (a.Title, a.Summary))
                .ToList();

            var articlesToTranslateForJson = batch.Select(a => new { title = a.Title, summary = a.Summary }).ToList();
            string combinedText = JsonSerializer.Serialize(articlesToTranslateForJson, s_jsonSerializerOptions);

            string systemPrompt = GetSystemPrompt(targetLanguage);
            ChatOptions chatOptions = options.BuildChatOptions();

            List<ChatMessage> messages =
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, $"Translate this JSON array to {Languages.GetDisplayName(targetLanguage)}. Maintain the JSON structure perfectly:\n{combinedText}"),
            ];

            StringBuilder resultBuilder = new();

            await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(messages, chatOptions)
                                .ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(update.Text))
                    resultBuilder.Append(update.Text);
            }
            string result = resultBuilder.ToString();

            // Extract the JSON array, discarding any reasoning or markdown the model emitted around it
            Match jsonMatch = Regex.Match(result, @"\[[\s\S]*\]");
            result = jsonMatch.Success ? jsonMatch.Value : result;

            result = SanitizeJsonResponse(result);

            try
            {
                List<TranslationDto>? translatedArticles = JsonSerializer.Deserialize<List<TranslationDto>>(result, s_jsonDeserializerOptions);

                if (translatedArticles != null)
                {
                    for (int i = 0; i < batch.Count; i++)
                    {
                        if (i < translatedArticles.Count)
                        {
                            batch[i].Title = translatedArticles[i].Title;
                            batch[i].Summary = translatedArticles[i].Summary;
                        }
                    }

                    if (options.UseResultsForDataset)
                        await translationRepository.SaveTranslationsAsync(batch, originals, targetLanguage);
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Error deserializing translation response. LLM response: {LlmResponse}", result);
            }
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
            // Heuristic: a '"' that is followed (ignoring spaces) by ':', ',', '}', ']', or EOF
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
                    while (j < json.Length && json[j] == ' ') j++;
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
