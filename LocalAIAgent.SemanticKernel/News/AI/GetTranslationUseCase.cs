using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.Chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;

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
        private readonly ChatHistory chatHistory = [];

        public async Task<List<NewsArticle>> TranslateArticleAsync(List<NewsArticle> articles, string targetLanguage)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<string> sourcesToTranslate = newsClientSettings.Where(s => s.RequiresTranslation).Select(s => s.Host).ToList();

            if (sourcesToTranslate.Count is 0) return articles;

            List<NewsArticle> articlesToTranslate = articles.Where(a => sourcesToTranslate.Contains(a.Source)).ToList();

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

        private async Task TranslateBatchAsync(List<NewsArticle> batch, string targetLanguage)
        {
            // Create combined text for batch translation
            List<string> texts = batch.SelectMany(a => new[] { $"Title: {a.Title}", $"Summary: {a.Summary}" })
                            .ToList();
            string combinedText = string.Join("\n\n", texts);

            string prompt = $"You are a professional translator. Translate the following news article titles and summaries into {targetLanguage}. " +
                           $"Keep the 'Title:' and 'Summary:' labels in English. " +
                           $"Do not change the format, only translate.";

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

            // Parse and update articles
            string[] translatedParts = result.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < batch.Count; i++)
            {
                string titlePart = translatedParts[i * 2];
                string summaryPart = translatedParts[(i * 2) + 1];

                batch[i].Title = titlePart.Replace("Title: ", "").Trim();
                batch[i].Summary = summaryPart.Replace("Summary: ", "").Trim();
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage)
        {
            string prompt = $"Translate the following text to {targetLanguage}." +
                "Respond with the translated text and nothing else.";

            OpenAIPromptExecutionSettings openAiSettings = options.GetOpenAIPromptExecutionSettings(
                prompt, allowFunctionUse: false);

            chatHistory.Clear();
            chatHistory.AddUserMessage(text);
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

            return result;
        }
    }
}
