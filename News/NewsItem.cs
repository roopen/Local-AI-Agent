using System.ServiceModel.Syndication;

namespace Local_AI_Agent.News
{
    public class NewsItem
    {
        public DateTimeOffset PublishDate { get; }
        public string Title { get; }
        public string? Summary { get; }
        public string? Link { get; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Title = syndicationItem.Title.Text;
            Summary = syndicationItem.Summary?.Text;
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
        }

        public override string ToString()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            return jsonString;
        }
    }
}
