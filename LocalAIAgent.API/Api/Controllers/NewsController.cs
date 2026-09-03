using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Application.News.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Relevancy = LocalAIAgent.Domain.Relevancy;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NewsController(
        INewsChatUseCase newsChatUseCase,
        IGetDatasetUseCase getDatasetUseCase,
        NewsMetrics newsMetrics,
        UserContext userContext) : ControllerBase
    {
        [HttpPost("GetExpandedNews")]
        public async Task<ActionResult<ExpandedNewsResult>> GetExpandedNews(
            [FromBody] string article,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out int userId))
                return Unauthorized();

            int? preferencesId = await userContext.UserPreferences
                .AsNoTracking()
                .Where(preferences => preferences.UserId == userId)
                .Select(preferences => (int?)preferences.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (preferencesId is null)
                return NotFound("User preferences not found.");

            newsMetrics.StartRecordingRequest();

            ExpandedNewsResult result = await newsChatUseCase.GetExpandedNewsAsync(article, preferencesId);

            newsMetrics.StopRecordingRequest();
            return Ok(result);
        }

        [HttpPost("Feedback")]
        public async Task<IActionResult> SubmitFeedback([FromBody] NewsFeedbackDto dto)
        {
            if (!User.TryGetUserId(out int userId))
                return Unauthorized();

            UserPreferences? preferences = await userContext.UserPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId);

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

        [Authorize(Roles = AuthRoles.Owner)]
        [HttpGet("Dataset")]
        public async Task<IActionResult> GetDataset([FromQuery] string? modelId, CancellationToken cancellationToken)
        {
            byte[]? zip = await getDatasetUseCase.GetDatasetZipAsync(modelId, cancellationToken);
            if (zip is null)
                return NotFound($"No dataset entries found for model '{modelId}'.");

            return File(zip, "application/zip", "dataset.zip");
        }
    }
}
