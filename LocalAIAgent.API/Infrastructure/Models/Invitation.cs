namespace LocalAIAgent.API.Infrastructure.Models;

public sealed class Invitation
{
    public int Id { get; set; }
    public required string TokenHash { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public int? RedeemedByUserId { get; set; }
    public User? RedeemedByUser { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
