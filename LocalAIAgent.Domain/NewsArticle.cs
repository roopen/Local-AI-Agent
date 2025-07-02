using System;

namespace LocalAIAgent.Domain
{
    public class NewsArticle
    {
        public required Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public required DateTime PublishedDate { get; set; }
    }
}