using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel.News.AI;
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
        IGetTranslationUseCase translationUseCase,
        IGetDatasetUseCase getDatasetUseCase,
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

        [AllowAnonymous]
        [HttpGet("Dataset")]
        public async Task<IActionResult> GetDataset(CancellationToken cancellationToken)
        {
            byte[] zip = await getDatasetUseCase.GetDatasetZipAsync(cancellationToken);
            return File(zip, "application/zip", "dataset.zip");
        }
    }
}