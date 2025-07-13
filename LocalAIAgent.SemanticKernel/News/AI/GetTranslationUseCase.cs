using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text.Json;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface IGetTranslationUseCase
    {
        Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage);
    }

    internal class GetTranslationUseCase(
        IEnumerable<BaseNewsClientSettings> newsClientSettings,
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
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

        private readonly ChatHistory chatHistory = [];

        public async Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<BaseNewsClientSettings> sourcesToTranslate = newsClientSettings.Where(s => s.RequiresTranslation).ToList();

            if (sourcesToTranslate.Count is 0) return articles;

            List<NewsArticle> articlesToTranslate = [];

            foreach (BaseNewsClientSettings source in sourcesToTranslate)
            {
                string host = source.Host;
                string sourceName = source.ClientName.Replace("Client", null).ToLowerInvariant();

                // Filter articles that belong to the current source and require translation
                articlesToTranslate.AddRange(articles.Where(a => a.Source.Contains(host) || a.Source.Contains(sourceName)));
            }

            if (articlesToTranslate.Count is 0) return articles;

            // Process articles in batches of 5
            for (int i = 0; i < articlesToTranslate.Count; i += 5)
            {
                int batchSize = Math.Min(5, articlesToTranslate.Count - i);
                List<NewsArticle> batch = articlesToTranslate.Skip(i).Take(batchSize).ToList();
                await TranslateBatchAsync(batch, targetLanguage);
            }
            stopwatch.Stop();
            Console.WriteLine($"GetTranslationUseCase: Translated {articlesToTranslate.Count} articles in {stopwatch.ElapsedMilliseconds} ms.");

            return articles;
        }

        private record TranslationDto(string Title, string Summary);

        private async Task TranslateBatchAsync(List<NewsArticle> batch, string targetLanguage)
        {
            var articlesToTranslateForJson = batch.Select(a => new { title = a.Title, summary = a.Summary }).ToList();
            string combinedText = JsonSerializer.Serialize(articlesToTranslateForJson, s_jsonSerializerOptions);

            string prompt = $"You are a professional translator. " +
                $"You must translate the 'Title' and 'Summary' fields of each JSON object in the following array into {targetLanguage}. " +
                "The incoming text may change language between fields, so you must translate each field independently. " +
                $"Respond with a valid JSON array of the translations, maintaining the same structure. " +
                $"Do not include any other text or formatting.";

            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt, allowFunctionUse: false);

            chatHistory.Clear();
            chatHistory.AddUserMessage(combinedText);
            string result = string.Empty;

            await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                                chatHistory,
                                openAiSettings,
                                kernel)
                                .ConfigureAwait(false))
            {
                if (string.IsNullOrEmpty(content.Content))
                    continue;

                result += content.Content;
            }

            // clean result from markdown
            result = result.Replace("```json", "").Replace("```", "").Trim();

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
    }
}
