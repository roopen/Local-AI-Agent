using LocalAIAgent.Domain.ValueObjects;

namespace LocalAIAgent.Domain
{
    public class User
    {
        public required UserId Id { get; set; }
        public required string Username { get; set; }
        public UserPreferences? Preferences { get; set; }
    }
}