using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface IGetTranslationUseCase
    {
        Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage);
    }

    internal class GetTranslationUseCase(
        IEnumerable<BaseNewsClientSettings> newsClientSettings,
        Kernel kernel,
        AIOptions options) : IGetTranslationUseCase
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
            List<BaseNewsClientSettings> sourcesToTranslate = newsClientSettings.Where(s => s.RequiresTranslation).ToList();

            if (sourcesToTranslate.Count is 0) return articles;

            List<NewsArticle> articlesToTranslate = [];

            foreach (BaseNewsClientSettings source in sourcesToTranslate)
            {
                string sourceName = source.ClientName.Replace("Client", null).ToLowerInvariant();

                // Filter articles that belong to the current source and require translation
                articlesToTranslate.AddRange(articles.Where(a => MatchesHost(a.Source, source.Host) || a.Source.Contains(sourceName)));
            }

            if (articlesToTranslate.Count is 0) return articles;

            // Process batches of 1 in parallel
            List<Task> batchTasks = [];
            for (int i = 0; i < articlesToTranslate.Count; i += 1)
            {
                List<NewsArticle> batch = articlesToTranslate.Skip(i).Take(1).ToList();
                batchTasks.Add(TranslateBatchAsync(batch, targetLanguage));
            }
            await Task.WhenAll(batchTasks);
            stopwatch.Stop();
            Console.WriteLine($"GetTranslationUseCase: Translated {articlesToTranslate.Count} articles in {stopwatch.ElapsedMilliseconds} ms.");

            return articles;
        }

        private record TranslationDto(string Title, string Summary);

        private async Task TranslateBatchAsync(List<NewsArticle> batch, string targetLanguage, int attempt = 0)
        {
            var articlesToTranslateForJson = batch.Select(a => new { title = a.Title, summary = a.Summary }).ToList();
            string combinedText = JsonSerializer.Serialize(articlesToTranslateForJson, s_jsonSerializerOptions);

            string systemPrompt = $@"
                <|think|>
                ## Role
                Translate news JSON objects into {targetLanguage}. 
                
                ## Critical Logic (<|channel>thought)
                For each article:
                1. Identify Source Language (e.g., Traditional Chinese).
                2. List 2-3 'Anchor Terms' (e.g., OPEC+, AFP, technical nouns) and their {targetLanguage} equivalents.
                3. Explicitly set internal state to {targetLanguage} mode.
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
                - Mode: {targetLanguage}.
                <channel|>
                [{{""title"": ""OPEC+: Energy Facility Repairs Are Time-Consuming"", ""summary"": ""AFP reports...""}}]
                [END EXAMPLE]
                <|turn>";

            ChatCompletionAgent agent = new()
            {
                Instructions = systemPrompt,
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false)),
            };

            ChatHistoryAgentThread thread = new();
            ChatMessageContent userMessage = new(AuthorRole.User, $"Translate this JSON array to {targetLanguage}. Maintain the JSON structure perfectly:\n{combinedText}");

            StringBuilder resultBuilder = new();

            await foreach (StreamingChatMessageContent? content in agent.InvokeStreamingAsync(userMessage, thread)
                                .ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(content.Content))
                    continue;

                resultBuilder.Append(content.Content);
            }
            string result = resultBuilder.ToString();

            // Extract the JSON array, discarding any reasoning or markdown the model emitted around it
            Match jsonMatch = Regex.Match(result, @"\[[\s\S]*\]");
            result = jsonMatch.Success ? jsonMatch.Value : result;

            result = SanitizeJsonResponse(result);

            //bool translationSuccessful = await VerifyTranslations(result, targetLanguage);

            //if (!translationSuccessful && attempt < 1) await TranslateBatchAsync(batch, targetLanguage, attempt + 1);

            //if (!translationSuccessful)
            //    batch = [];

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
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing translation response: {ex.Message}");
                Console.WriteLine($"LLM Response: {result}");
            }
        }

        private async Task<bool> VerifyTranslations(string translations, string targetLanguage)
        {
            string systemPrompt = $@"
                <|turn>system
                Use the <|think> block ONLY for internal reasoning. 
                Do NOT reveal the content of the <|think> block in the final output.
                
                Your task:
                - You will receive a JSON array of article objects from the user.
                - For EACH article, verify whether BOTH 'title' and 'summary' are written in {targetLanguage}.
                - Return a JSON array where each element corresponds to the input article and has the structure:
                  {{ ""isValid"": true }} or {{ ""isValid"": false }}.
                
                Rules:
                1. Output MUST be a valid JSON array.
                2. The final JSON MUST contain exactly one object per input article.
                3. The final JSON MUST be entirely in {targetLanguage}.
                4. Do NOT include markdown, comments, explanations, or any text outside the JSON array.
                5. Do NOT output <|think> or <|chain> blocks in the final answer.
                6. Your response MUST start with '[' and end with ']'.
                
                [EXAMPLE]
                Input:
                [
                  {{ ""title"": ""Mímir Kristjánsson sendte truende meldinger"", ""summary"": ""Stortingsrepresentanten beklager."" }},
                  {{ ""title"": ""Hello world"", ""summary"": ""This is English"" }}
                ]
                
                Output:
                <|think>
                Identify languages:
                - Article 1: Norwegian → not {targetLanguage}
                - Article 2: English → not {targetLanguage}
                </think>
                [
                  {{ ""isValid"": false }},
                  {{ ""isValid"": false }}
                ]
                [END EXAMPLE]
                <|turn>
                ";


            ChatCompletionAgent verifyAgent = new()
            {
                Instructions = systemPrompt,
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false)),
            };

            ChatHistoryAgentThread verifyThread = new();
            ChatMessageContent verifyMessage = new(AuthorRole.User,
                $"<|turn>user\r\n" +
                $"Verify that ALL of the following texts are in {targetLanguage}:\r\n" +
                $"{translations}<|turn>\r\n" +
                $"<|turn>model");
            StringBuilder resultBuilder = new();

            await foreach (StreamingChatMessageContent? content in verifyAgent.InvokeStreamingAsync(verifyMessage, verifyThread)
                                .ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(content.Content))
                    continue;

                resultBuilder.Append(content.Content);
            }
            string result = resultBuilder.ToString();

            // Extract the JSON array; fall back to wrapping a lone object in brackets.
            Match jsonMatch = Regex.Match(result, @"\[[\s\S]*\]");
            if (jsonMatch.Success)
            {
                result = jsonMatch.Value;
            }
            else
            {
                Match objMatch = Regex.Match(result, @"\{[\s\S]*\}");
                result = objMatch.Success ? $"[{objMatch.Value}]" : result;
            }

            try
            {
                return JsonSerializer.Deserialize<TranslationVerificationResult[]>(result, s_jsonDeserializerOptions)?.All(r => r.IsValid) ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Returns true when <paramref name="articleSource"/> belongs to the same site as
        /// <paramref name="settingsHost"/>, including sibling subdomains.
        /// e.g. "news.ltn.com.tw" matches settings host "www.ltn.com.tw" via shared parent "ltn.com.tw".
        /// </summary>
        private static bool MatchesHost(string articleSource, string settingsHost)
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
        private static string SanitizeJsonResponse(string json)
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
