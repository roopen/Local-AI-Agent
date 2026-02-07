namespace LocalAIAgent.API.Infrastructure.Mapping
{
    internal static class UserMapping
    {
        public static Domain.User MapToDomainUser(this Models.User user)
        {
            return new Domain.User
            {
                Id = user.Id,
                Fido2Id = user.Fido2Id,
                Username = user.Username,
                Preferences = user.Preferences?.MapToDomainUserPreferences()
            };
        }

        public static Domain.UserPreferences MapToDomainUserPreferences(this Models.UserPreferences preferences)
        {
            return new Domain.UserPreferences
            {
                Prompt = preferences.Prompt,
                Interests = preferences.Interests,
                Dislikes = preferences.Dislikes
            };
        }
    }
}
