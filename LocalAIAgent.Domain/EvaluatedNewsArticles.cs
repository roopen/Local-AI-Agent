namespace LocalAIAgent.Domain
{
    public class EvaluatedNewsArticles
    {
        public required List<NewsArticle> NewsArticles { get; set; }
        public decimal HighRelevancyPercentage =>
            NewsArticles.Count == 0 ? 0 :
            (decimal)NewsArticles.Count(article => article.Relevancy == Relevancy.High) / NewsArticles.Count * 100;

        public decimal MediumRelevancyPercentage =>
            NewsArticles.Count == 0 ? 0 :
            (decimal)NewsArticles.Count(article => article.Relevancy == Relevancy.Medium) / NewsArticles.Count * 100;

        public decimal LowRelevancyPercentage { get; set; }
    }
}