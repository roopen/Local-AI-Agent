namespace LocalAIAgent.API.Api.Controllers.Serialization;

public class UserPreferenceDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public List<string> Interests { get; set; } = [];
    public List<string> Dislikes { get; set; } = [];
    public string TargetLanguage { get; set; } = "en";
    public List<string> DisabledFeedSources { get; set; } = [];
}