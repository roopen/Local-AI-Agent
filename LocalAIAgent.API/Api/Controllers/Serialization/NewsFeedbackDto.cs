namespace LocalAIAgent.API.Api.Controllers.Serialization;

public class NewsFeedbackDto
{
    public required int UserId { get; set; }
    public required string ArticleLink { get; set; }
    public required string ArticleTitle { get; set; }
    public required string ArticleSummary { get; set; }
    public required bool IsLiked { get; set; }
    public string? Reason { get; set; }
}
