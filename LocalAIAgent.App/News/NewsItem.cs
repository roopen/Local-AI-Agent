using System.ServiceModel.Syndication;

namespace LocalAIAgent.App.News
{
    public class NewsItem
    {
        public DateTimeOffset PublishDate { get; }
        public string? Content { get; }
        public string? Link { get; }
        public string? Source { get; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Content = syndicationItem.Title?.Text + syndicationItem.Summary?.Text;
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
            Source = string.IsNullOrWhiteSpace(Link) ? null : new Uri(Link).DnsSafeHost;
        }

        public override string ToString()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            return jsonString;
        }
    }
}
