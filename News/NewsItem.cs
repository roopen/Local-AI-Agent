using System.ServiceModel.Syndication;

namespace Local_AI_Agent.News
{
    public class NewsItem
    {
        public readonly DateTimeOffset PublishDate;
        public readonly string Title;
        public readonly string? Summary;
        public readonly string? Content;
        public readonly string? Link;

        public NewsItem(SyndicationItem syndicationItem)
        {
            Title = syndicationItem.Title.Text;
            Content = syndicationItem.Content?.ToString();
            Summary = syndicationItem.Summary?.Text;
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
        }

        public override string ToString()
        {
            return $"{Title} ({PublishDate})\n{Summary}\n{Content}\n";
        }
    }
}
