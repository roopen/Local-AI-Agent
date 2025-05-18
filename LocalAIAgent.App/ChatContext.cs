using System.ServiceModel.Syndication;

namespace LocalAIAgent.App
{
    /// <summary>
    /// Contains the context, state, and preferences of the chat session.
    /// </summary>
    internal class ChatContext
    {
        public List<string> UserDislikes { get; set; } = [];

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
            if (UserDislikes.Any(dislike => item.Title is not null &&
                    item.Title.Text.Contains(dislike, StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }
            if (UserDislikes.Any(dislike => item.Summary is not null &&
                    item.Summary.Text.Contains(dislike, StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }

            return true;
        }
    }
}
