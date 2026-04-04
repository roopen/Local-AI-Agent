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
                "Rules for the <|channel>thought block:\n" +
                "- NO 'Final Check' or introductory sentences.\n" +
                "- Use ONLY the following 4-line compressed format per article:\n" +
                "  Art [Index]: {Topic Name}\n" +
                "  Likes: {List found likes or 'None'}\n" +
                "  Dislikes: {List found dislikes or 'None'}\n" +
                "  Result: {Brief logic for High/Low}\n" +
                "- Total thought block must be under 40 words per article." +
                "DO NOT write sentences. DO NOT summarize the story." +
                "Use the <|channel>thought block to perform a step-by-step analysis for each article. " +
                "Compare the article content against likes, dislikes, and the user's prompt. " +
                "If an article matches both a like and a dislike, explicitly resolve the conflict by prioritizing the dislike.\n\n" +
                "After the <channel|> tag, respond ONLY with a valid JSON array — no markdown, no extra text, no trailing commas.\n" +
                "Include one entry for EVERY article in the input, in order.\n" +
                "Use this exact structure for the JSON:\n" +
                "[\n" +
                "  {\"ArticleIndex\": 0, \"Relevancy\": \"High\", \"Topic\": \"Technology\", \"Reasoning\": \"Matches your interest in AI breakthroughs.\"}\n" +
                "]\n\n" +
                "Rules:\n" +
                "- ArticleIndex is the 0-based index of the article as presented in the input\n" +
                "- Relevancy must be exactly one of: High, Low\n" +
                "- Topic is a single short label (e.g., Technology, Politics). Use ONE label only — no slashes.\n" +
                "- Reasoning is a brief, user-friendly explanation for the UI. (Keep internal logic in the thought block).\n\n" +
                "User preferences are as follows:\n" +
                "User's dislikes: \n" + dislikes + "\n" +
                "User's likes: \n" + likes + "\n" +
                "User's prompt: \n" + Prompt;
        }
    }
}
