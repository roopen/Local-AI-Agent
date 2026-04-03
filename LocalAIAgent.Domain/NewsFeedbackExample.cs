namespace LocalAIAgent.Domain
{
    public class NewsFeedbackExample
    {
        public required string ArticleLink { get; set; }
        public required string Title { get; set; }
        public required string Summary { get; set; }
        public required bool IsLiked { get; set; }
    }
}
