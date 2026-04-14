using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController(
    IGetLMStudioModelsUseCase getLMStudioModelsUseCase,
    IDownloadLLMModelUseCase downloadLLMModelUseCase) : ControllerBase
{
    [HttpGet("models")]
    public async Task<ActionResult<List<LMStudioModel>>> GetModels()
    {
        List<LMStudioModel> models = await getLMStudioModelsUseCase.GetModelsAsync();
        return Ok(models);
    }

    [HttpPost("models/download")]
    [Authorize]
    public async Task<IActionResult> DownloadModel([FromBody] string modelId)
    {
        bool success = await downloadLLMModelUseCase.DownloadModelAsync(modelId);
        return success ? Ok() : StatusCode(502, "LM Studio failed to start the download.");
    }
}
