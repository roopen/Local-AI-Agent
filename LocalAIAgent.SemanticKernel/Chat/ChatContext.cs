namespace LocalAIAgent.SemanticKernel.Chat
{
    /// <summary>
    /// Contains the context, state, and preferences of the chat session.
    /// </summary>
    public class ChatContext
    {
        public string UserPrompt { get; set; } = string.Empty;
        public List<string> UserDislikes { get; set; } = [];
        public List<string> UserInterests { get; set; } = [];

        public string GetUserDislikesAsString()
        {
            if (UserDislikes.Count > 0)
            {
                return string.Join(", ", UserDislikes);
            }
            return string.Empty;
        }

        public string GetUserInterestsAsString()
        {
            if (UserInterests.Count > 0)
            {
                return string.Join(", ", UserInterests);
            }
            return string.Empty;
        }
    }
}
