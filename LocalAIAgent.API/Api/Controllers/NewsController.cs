using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NewsController(
        IGetNewsUseCase getNewsUseCase,
        IGetUserUseCase getUserUseCase,
        NewsMetrics newsMetrics) : ControllerBase
    {
        [HttpGet("{userId}")]
        public async Task<ActionResult<List<NewsItem>>> GetNews(int userId)
        {
            newsMetrics.StartRecordingRequest();

            User? user = await getUserUseCase.GetUserById(userId);
            if (user == null)
                return BadRequest($"User with ID {userId} not found.");
            if (user.Preferences is null)
                return BadRequest("User preferences are not set.");

            List<NewsItem> news = await getNewsUseCase.GetAsync(user.Preferences);

            newsMetrics.RecordNewsArticleCount(news.Count);
            newsMetrics.StopRecordingRequest();
            return Ok(news);
        }

        [HttpPost("GetNewsV2")]
        public async Task<ActionResult<EvaluatedNewsArticles>> GetNewsV2(int userId)
        {
            newsMetrics.StartRecordingRequest();

            User? user = await getUserUseCase.GetUserById(userId);
            if (user == null)
                return BadRequest($"User with ID {userId} not found.");
            if (user.Preferences is null)
                return BadRequest("User preferences are not set.");

            EvaluatedNewsArticles news = await getNewsUseCase.GetAsyncV2(user.Preferences);

            newsMetrics.RecordNewsArticleCount(news.NewsArticles.Count);
            newsMetrics.StopRecordingRequest();
            return Ok(news);
        }
    }
}