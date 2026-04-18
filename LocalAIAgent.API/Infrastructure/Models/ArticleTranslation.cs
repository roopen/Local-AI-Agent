namespace LocalAIAgent.API.Infrastructure.Models;

public class ArticleTranslation
{
    public int Id { get; set; }
    public required string ArticleLink { get; set; }
    public required string OriginalTitle { get; set; }
    public required string OriginalSummary { get; set; }
    public required string TranslatedTitle { get; set; }
    public required string TranslatedSummary { get; set; }
    public required string TargetLanguage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
