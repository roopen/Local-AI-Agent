namespace LocalAIAgent.API.Infrastructure.Models;

public class CustomFeed
{
    public int Id { get; set; }
    public required List<string> Urls { get; set; } = [];
    public required string DisplayName { get; set; }
    public required string Language { get; set; }
    public bool Enabled { get; set; } = true;

    public string? LastFetchErrorMessage { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserPreferencesId { get; set; }
    public UserPreferences? UserPreferences { get; set; }
}
