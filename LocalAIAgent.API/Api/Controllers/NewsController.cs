using LocalAIAgent.API.Metrics;
using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NewsController(
        INewsChatUseCase newsChatUseCase,
        NewsMetrics newsMetrics) : ControllerBase
    {
        [HttpPost("GetExpandedNews")]
        public async Task<ActionResult<ExpandedNewsResult>> GetExpandedNews([FromBody] string article)
        {
            newsMetrics.StartRecordingRequest();

            ExpandedNewsResult result = await newsChatUseCase.GetExpandedNewsAsync(article);

            newsMetrics.StopRecordingRequest();
            return Ok(result);
        }
    }
}