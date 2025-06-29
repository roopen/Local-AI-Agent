using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;

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

        internal bool IsArticleRelevant(SyndicationItem item)
        {
            if (item is not null)
            {
                return SimpleWordFilter(item);
            }

            return false;
        }

        /// <summary>
        /// Filters out articles based on simple word matching.
        /// </summary>
        private bool SimpleWordFilter(SyndicationItem item)
        {
            foreach (string dislike in UserDislikes)
            {
                string pattern = $@"\b{Regex.Escape(dislike)}\b"; // Ensure whole word match

                foreach (SyndicationCategory category in item.Categories)
                {
                    if (Regex.IsMatch(category.Name, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }

                if (Regex.IsMatch(item.Title.Text, pattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }

                if (item.Summary is not null && Regex.IsMatch(item.Summary.Text, pattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
