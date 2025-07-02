namespace LocalAIAgent.Domain
{
    public class UserPreferences
    {
        public required string Prompt { get; set; }
        public required List<string> Interests { get; set; }
        public required List<string> Dislikes { get; set; }

        public string GetUserDislikesAsString()
        {
            if (Dislikes.Count > 0)
            {
                return string.Join(", ", Dislikes);
            }
            return string.Empty;
        }

        public string GetUserInterestsAsString()
        {
            if (Interests.Count > 0)
            {
                return string.Join(", ", Interests);
            }
            return string.Empty;
        }
    }
}
