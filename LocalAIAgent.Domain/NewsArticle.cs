namespace LocalAIAgent.Domain
{
    public class NewsArticle
    {
        public required string Title { get; set; }
        public required string Summary { get; set; }
        public required DateTime PublishedDate { get; set; }
        public required string Link { get; set; }
        public required string Source { get; set; }
        public required HashSet<string> Categories { get; set; }
        public required Relevancy Relevancy { get; set; }
        public string? Reasoning { get; set; }
        public string? Topic { get; set; }
        public string? Event { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
    }

    public enum Relevancy
    {
        High,
        Medium,
        Low
    }
}