using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Application.News.Reader;
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
        UserContext userContext,
        ILogger<NewsController>? logger = null) : ControllerBase
    {
        [HttpPost("ReadArticle")]
        public async Task<ActionResult<ReadArticleResult>> ReadArticle(
            [FromBody] ReadArticleRequest request,
            [FromServices] IReadArticleUseCase reader,
            CancellationToken cancellationToken)
        {
            if (!User.TryGetUserId(out int userId)) return Unauthorized();
            if (!ArticleUrlPolicy.IsValid(request.Url))
                return Problem("A public HTTP(S) article URL on port 80 or 443 is required.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = "article_invalid_url" });
            UserPreferences? preferences = await userContext.UserPreferences.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (preferences is null) return Problem("Save your news preferences before opening an article.", statusCode: 404,
                extensions: new Dictionary<string, object?> { ["code"] = "article_preferences_missing" });
            if (Request.Headers.Accept.Any(value => value?.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase) == true))
                return await StreamArticleAsync(reader, request, userId, preferences, cancellationToken);
            try
            {
                return Ok(await reader.ReadAsync(request, userId, preferences.Id, preferences.TargetLanguage, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Dictionary<string, object?> failure = ReaderFailure(ex, "fetching");
                int status = (int)failure["status"]!;
                failure.Remove("status");
                return Problem("Article retrieval failed. See diagnostic details.", statusCode: status, extensions: failure);
            }
        }

        private Dictionary<string, object?> ReaderFailure(Exception error, string phase)
        {
            string code = error is ArticleReaderUnavailableException known ? known.Code
                : error is OperationCanceledException ? "article_timeout" : "article_reader_failed";
            string requestId = HttpContext.TraceIdentifier;
            logger?.LogWarning("Article reader failed: {Code}, phase {Phase}, request {RequestId}, exception {ExceptionType}",
                code, phase, requestId, error.GetType().Name);
            return new()
            {
                ["code"] = code, ["status"] = error is OperationCanceledException ? 504 : 503,
                ["phase"] = phase, ["requestId"] = requestId,
                ["diagnostic"] = ArticleReaderDiagnostics.Describe(error),
            };
        }

        private async Task<ActionResult<ReadArticleResult>> StreamArticleAsync(IReadArticleUseCase reader,
            ReadArticleRequest request, int userId, UserPreferences preferences, CancellationToken cancellationToken)
        {
            string phase = "starting";
            Response.ContentType = "application/x-ndjson";
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            async Task SendAsync(object value)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                await Response.WriteAsync(json + "\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            try
            {
                ReadArticleResult result = await reader.ReadWithProgressAsync(request, userId, preferences.Id,
                    preferences.TargetLanguage, progress => { phase = progress.Phase; return SendAsync(new { type = "progress", progress }); }, cancellationToken);
                await SendAsync(new { type = "result", result });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Dictionary<string, object?> failure = ReaderFailure(ex, phase);
                failure["type"] = "error";
                await SendAsync(failure);
            }
            return new EmptyResult();
        }

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
        [HttpGet("Dataset/Models", Name = nameof(GetDatasetModels))]
        public async Task<ActionResult<List<string>>> GetDatasetModels(CancellationToken cancellationToken)
        {
            List<string> models = await userContext.NewsEvaluationEntries
                .AsNoTracking()
                .Where(entry => entry.UseInDataset && entry.ModelUsed != "")
                .Select(entry => entry.ModelUsed)
                .Distinct()
                .OrderBy(model => model)
                .ToListAsync(cancellationToken);
            return Ok(models);
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
