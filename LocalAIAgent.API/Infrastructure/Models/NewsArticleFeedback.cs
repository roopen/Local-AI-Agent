namespace LocalAIAgent.API.Infrastructure.Models;

public class NewsArticleFeedback
{
    public int Id { get; set; }
    public required string ArticleLink { get; set; }
    public required string ArticleTitle { get; set; }
    public required string ArticleSummary { get; set; }
    public required bool IsLiked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserPreferencesId { get; set; }
    public UserPreferences? UserPreferences { get; set; }
}
