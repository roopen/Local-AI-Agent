namespace LocalAIAgent.Domain
{
    public class UserPreferences
    {
        public required string Prompt { get; set; }
        public required List<string> Interests { get; set; }
        public required List<string> Dislikes { get; set; }
        public List<NewsFeedbackExample> FeedbackExamples { get; set; } = [];

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

        public string GetFeedbackExamplesAsString()
        {
            if (FeedbackExamples.Count == 0)
                return string.Empty;

            System.Text.StringBuilder sb = new();

            List<NewsFeedbackExample> liked = FeedbackExamples.Where(f => f.IsLiked).ToList();
            List<NewsFeedbackExample> disliked = FeedbackExamples.Where(f => !f.IsLiked).ToList();

            if (liked.Count > 0)
            {
                sb.AppendLine("Articles the user explicitly liked (treat as High relevancy reference):");
                foreach (NewsFeedbackExample f in liked)
                    sb.AppendLine($"- \"{f.Title}\": {f.Summary}");
            }

            if (disliked.Count > 0)
            {
                sb.AppendLine("Articles the user explicitly disliked (treat as Low relevancy reference):");
                foreach (NewsFeedbackExample f in disliked)
                    sb.AppendLine($"- \"{f.Title}\": {f.Summary}");
            }

            return sb.ToString();
        }
    }
}
