namespace LocalAIAgent.API.Infrastructure.Models;

public class UserPreferences
{
    public int Id { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public List<string> Interests { get; set; } = [];
    public List<string> Dislikes { get; set; } = [];
    public int UserId { get; set; }
    public User? User { get; set; }
}