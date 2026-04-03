using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                existing.IsLiked = dto.IsLiked;
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
                    UserPreferencesId = preferences.Id
                });
            }

            await userContext.SaveChangesAsync();
            return Ok();
        }
    }
}