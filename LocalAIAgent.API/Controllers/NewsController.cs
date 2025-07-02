using LocalAIAgent.API.UseCases;
using LocalAIAgent.SemanticKernel;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController(
        IGetNewsUseCase getNewsUseCase,
        IGetUserUseCase getUserUseCase) : ControllerBase
    {
        [HttpGet("{userId}")]
        public async Task<ActionResult<List<NewsItem>>> GetNews(int userId)
        {
            Domain.User? user = await getUserUseCase.GetUserById(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found.");
            }

            List<NewsItem> news = await getNewsUseCase.GetAsync(user.Preferences);
            return Ok(news);
        }
    }
}