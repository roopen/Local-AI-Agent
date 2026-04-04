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

        public string BuildSystemPrompt()
        {
            string likes = Interests.Count > 0 ? string.Join(", ", Interests) : string.Empty;
            string dislikes = Dislikes.Count > 0 ? string.Join(", ", Dislikes) : string.Empty;

            return
                "Evaluate the following news articles based on user preferences.\n" +
                "If the article matches a like and a dislike, prioritize the dislike.\n" +
                "Respond ONLY with a valid JSON array \u2014 no markdown, no extra text, no trailing commas.\n" +
                "Include one entry for EVERY article in the input, in order.\n" +
                "Use this exact structure:\n" +
                "[\n" +
                "  {\"ArticleIndex\": 0, \"Relevancy\": \"High\", \"Topic\": \"Technology\", \"Event\": \"AI arms race between US and China\", \"Reasoning\": \"Matches user interest in AI and geopolitics.\"},\n" +
                "  {\"ArticleIndex\": 1, \"Relevancy\": \"Low\", \"Topic\": \"Sports\", \"Event\": \"\", \"Reasoning\": \"User has no interest in sports.\"}\n" +
                "]\n" +
                "Rules:\n" +
                "- ArticleIndex is the 0-based index of the article as presented in the input\n" +
                "- Relevancy must be exactly one of: High, Low\n" +
                "- Topic is a single short label for the subject area (e.g. Technology, Politics, Finance). Use ONE topic only — never combine multiple topics with slashes.\n" +
                "- Event is the name of an ongoing real-world event the article is part of (e.g. war, election, crisis); leave empty string if the article is not about an ongoing event\n" +
                "- Reasoning is a brief explanation of why the article received its Relevancy score\n" +
                "User preferences are as follows:\n" +
                "User's dislikes: \n" + dislikes + "\n" +
                "User's likes: \n" + likes + "\n" +
                "User's prompt: \n" + Prompt;
        }
    }
}
