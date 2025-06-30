namespace LocalAIAgent.API.Infrastructure;

public class UserPreference
{
    public int Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public List<string> Interests { get; set; } = [];
    public List<string> Dislikes { get; set; } = [];
    public int UserId { get; set; }
    public User? User { get; set; }
}