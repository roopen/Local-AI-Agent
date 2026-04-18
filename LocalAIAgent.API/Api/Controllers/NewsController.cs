using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using Relevancy = LocalAIAgent.Domain.Relevancy;
using LocalAIAgent.API.Infrastructure.Mapping;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel.News.AI;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NewsController(
        INewsChatUseCase newsChatUseCase,
        IGetTranslationUseCase translationUseCase,
        NewsMetrics newsMetrics,
        UserContext userContext) : ControllerBase
    {
        [HttpPost("GetExpandedNews")]
        public async Task<ActionResult<ExpandedNewsResult>> GetExpandedNews([FromBody] string article)
        {
            newsMetrics.StartRecordingRequest();

            ExpandedNewsResult result = await newsChatUseCase.GetExpandedNewsAsync(article);

            newsMetrics.StopRecordingRequest();
            return Ok(result);
        }

        [HttpPost("Feedback")]
        public async Task<IActionResult> SubmitFeedback([FromBody] NewsFeedbackDto dto)
        {
            UserPreferences? preferences = await userContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == dto.UserId);

            if (preferences is null)
                return NotFound("User preferences not found.");

            string newRelevancy = dto.IsLiked ? nameof(Relevancy.High) : nameof(Relevancy.Low);

            NewsEvaluationEntry? existing = await userContext.NewsEvaluationEntries
                .FirstOrDefaultAsync(e => e.UserPreferencesId == preferences.Id && e.ArticleLink == dto.ArticleLink);

            if (existing is not null)
            {
                existing.Relevancy = newRelevancy;
                existing.Reasoning = dto.Reason;
            }
            else
            {
                userContext.NewsEvaluationEntries.Add(new NewsEvaluationEntry
                {
                    ArticleLink = dto.ArticleLink,
                    ArticleTitle = dto.ArticleTitle,
                    ArticleSummary = dto.ArticleSummary,
                    ArticleTopic = dto.ArticleTopic,
                    ArticleSource = string.Empty,
                    Relevancy = newRelevancy,
                    Reasoning = dto.Reason,
                    UserPreferencesId = preferences.Id
                });
            }

            await userContext.SaveChangesAsync();
            return Ok();
        }

        private static string BuildEntry(string systemPrompt, string userContent, string assistantContent)
        {
            string messagesJson = JsonSerializer.Serialize(new { role = "system", content = systemPrompt }) + "," +
                                  JsonSerializer.Serialize(new { role = "user", content = userContent }) + "," +
                                  JsonSerializer.Serialize(new { role = "assistant", content = assistantContent });
            return "{\"messages\":[" + messagesJson + "]}";
        }

        [AllowAnonymous]
        [HttpGet("EvaluationDataset")]
        public async Task<IActionResult> GetEvaluationDataset()
        {
            List<UserPreferences> allPreferences = await userContext.UserPreferences
                .Include(p => p.EvaluationEntries)
                .Where(p => p.EvaluationEntries.Count > 0)
                .ToListAsync();

            StringBuilder sb = new();

            foreach (UserPreferences preferences in allPreferences)
            {
                Domain.UserPreferences userPreferences = preferences.MapToDomainUserPreferences();
                string systemPrompt = userPreferences.BuildSystemPrompt();

                HashSet<string> knownTopics = new(StringComparer.OrdinalIgnoreCase);

                int offset = 0;
                while (offset < preferences.EvaluationEntries.Count)
                {
                    int batchSize = Random.Shared.Next(1, 4);
                    NewsEvaluationEntry[] batch = preferences.EvaluationEntries.Skip(offset).Take(batchSize).ToArray();
                    offset += batchSize;

                    string topicsContext = EvaluateNewsUseCase.FormatKnownTopics(knownTopics);

                    string userContent = topicsContext + string.Join("\n---ARTICLE SEPARATOR---\n",
                        batch.Select((e, i) =>
                        {
                            string source = Uri.TryCreate(e.ArticleLink, UriKind.Absolute, out Uri? uri)
                                ? uri.DnsSafeHost
                                : e.ArticleLink;
                            return $"Article {i}:\n{e.ArticleTitle}\n\n{e.ArticleSummary}\nSource: {source}\n";
                        }));

                    foreach (NewsEvaluationEntry e in batch)
                        if (!string.IsNullOrWhiteSpace(e.ArticleTopic))
                            knownTopics.Add(e.ArticleTopic.Trim());

                    string thinkBlock = BuildEvaluationThinkBlock(batch);
                    string assistantContent = thinkBlock + JsonSerializer.Serialize(
                        batch.Select((e, i) => new
                        {
                            ArticleIndex = i,
                            e.Relevancy,
                            Topic = e.ArticleTopic ?? string.Empty,
                        }));

                    sb.AppendLine(BuildEntry(systemPrompt, userContent, assistantContent));
                    sb.AppendLine();
                }
            }

            // Translation dataset — group stored translations by target language and emit batches of 3
            List<ArticleTranslation> allTranslations = await userContext.ArticleTranslations
                .Where(t => t.OriginalTitle != null && t.OriginalSummary != null)
                .OrderBy(t => t.TargetLanguage)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync();

            foreach (IGrouping<string, ArticleTranslation> group in allTranslations.GroupBy(t => t.TargetLanguage))
            {
                string translationSystemPrompt = translationUseCase.GetSystemPrompt(group.Key);
                List<ArticleTranslation> translations = [.. group];

                for (int i = 0; i < translations.Count; i += 3)
                {
                    ArticleTranslation[] batch = translations.Skip(i).Take(3).ToArray();

                    string userContent = $"Translate this JSON array to {group.Key}. Maintain the JSON structure perfectly:\n" +
                        JsonSerializer.Serialize(batch.Select(t => new { title = t.OriginalTitle, summary = t.OriginalSummary }));

                    string assistantContent = JsonSerializer.Serialize(
                        batch.Select(t => new { title = t.TranslatedTitle, summary = t.TranslatedSummary }));

                    sb.AppendLine(BuildEntry(translationSystemPrompt, userContent, assistantContent));
                    sb.AppendLine();
                }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/x-ndjson", "evaluation_dataset.jsonl");
        }

        private static string BuildEvaluationThinkBlock(NewsEvaluationEntry[] batch)
        {
            StringBuilder sb = new();
            for (int i = 0; i < batch.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(batch[i].Reasoning))
                    sb.AppendLine($"Article {i}: {batch[i].Reasoning}");
            }

            if (sb.Length == 0)
                return string.Empty;

            return $"<|think|>\n{sb}<|end|>\n";
        }
    }
}