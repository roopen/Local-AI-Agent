using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
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

            Domain.User? user = await getUserUseCase.GetUserById(userId);
            if (user == null)
                return BadRequest($"User with ID {userId} not found.");
            if (user.Preferences is null)
                return BadRequest("User preferences are not set.");

            List<NewsItem> news = await getNewsUseCase.GetAsync(user.Preferences);

            newsMetrics.StopRecordingRequest();
            return Ok(news);
        }
    }
}