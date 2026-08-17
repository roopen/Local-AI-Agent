using LocalAIAgent.Domain.ValueObjects;

namespace LocalAIAgent.Domain
{
    public enum UserRole
    {
        Member,
        Owner,
    }

    public class User
    {
        public required UserId Id { get; set; }
        public required Fido2Id Fido2Id { get; set; }
        public required string Username { get; set; }
        public UserRole Role { get; set; }
        public bool IsDisabled { get; set; }
        public UserPreferences? Preferences { get; set; }
    }
}
