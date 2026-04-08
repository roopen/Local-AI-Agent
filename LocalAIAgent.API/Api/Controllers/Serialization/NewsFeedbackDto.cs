namespace LocalAIAgent.API.Api.Controllers.Serialization;

public class NewsFeedbackDto
{
    public required int UserId { get; set; }
    public required string ArticleLink { get; set; }
    public required string ArticleTitle { get; set; }
    public required string ArticleSummary { get; set; }
    public required string ArticleTopic { get; set; }
    public required bool IsLiked { get; set; }
    public string? Reason { get; set; }
    public List<string>? SelectedLikes { get; set; }
    public List<string>? SelectedDislikes { get; set; }
}
