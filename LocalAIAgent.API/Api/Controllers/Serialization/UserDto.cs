namespace LocalAIAgent.API.Api.Controllers.Serialization;

public class UserDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public required string Role { get; set; }
}
