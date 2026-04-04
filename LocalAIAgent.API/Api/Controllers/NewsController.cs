using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Mapping;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel.News.AI;
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

            NewsArticleFeedback? existing = await userContext.NewsFeedback
                .FirstOrDefaultAsync(f => f.UserPreferencesId == preferences.Id && f.ArticleLink == dto.ArticleLink);

            if (existing is not null)
            {
                if (existing.IsLiked == dto.IsLiked)
                {
                    userContext.NewsFeedback.Remove(existing);
                    await userContext.SaveChangesAsync();
                    return Ok();
                }

                existing.IsLiked = dto.IsLiked;
                existing.Reason = dto.Reason ?? string.Empty;
                existing.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                userContext.NewsFeedback.Add(new NewsArticleFeedback
                {
                    ArticleLink = dto.ArticleLink,
                    ArticleTitle = dto.ArticleTitle,
                    ArticleSummary = dto.ArticleSummary,
                    IsLiked = dto.IsLiked,
                    Reason = dto.Reason ?? string.Empty,
                    UserPreferencesId = preferences.Id
                });
            }

            await userContext.SaveChangesAsync();
            return Ok();
        }

        [AllowAnonymous]
        [HttpGet("FineTuningDataset")]
        public async Task<IActionResult> GetFineTuningDataset()
        {
            const int BatchSize = 5;

            List<UserPreferences> allPreferences = await userContext.UserPreferences
                .Include(p => p.FeedbackExamples)
                .Where(p => p.FeedbackExamples.Count > 0)
                .ToListAsync();

            StringBuilder sb = new();

            foreach (UserPreferences preferences in allPreferences)
            {
                Domain.UserPreferences userPreferences = preferences.MapToDomainUserPreferences();
                string systemPrompt = userPreferences.BuildSystemPrompt();

                foreach (NewsArticleFeedback[] batch in preferences.FeedbackExamples.Chunk(BatchSize))
                {
                    string userContent = string.Join("\n---ARTICLE SEPARATOR---\n",
                        batch.Select((f, i) =>
                        {
                            string source = Uri.TryCreate(f.ArticleLink, UriKind.Absolute, out Uri? uri)
                                ? uri.DnsSafeHost
                                : f.ArticleLink;
                            return $"Article {i}:\n{f.ArticleTitle}\n\n{f.ArticleSummary}\nSource: {source}\n";
                        }));

                    string assistantContent = JsonSerializer.Serialize(
                        batch.Select((f, i) => new { ArticleIndex = i, Relevancy = f.IsLiked ? "High" : "Low" }));

                    var entry = new
                    {
                        messages = new object[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userContent },
                            new { role = "assistant", content = assistantContent }
                        }
                    };

                    sb.AppendLine(JsonSerializer.Serialize(entry));
                }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/x-ndjson", "finetuning_dataset.jsonl");
        }
    }
}