using LocalAIAgent.SemanticKernel.RAG;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using System.ServiceModel.Syndication;
using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.News
{
    public class NewsItem : BaseVectorData
    {
        [VectorStoreRecordKey]
        [JsonIgnore]
        public string Id { get; }
        public DateTimeOffset PublishDate { get; }

        public string Title { get; }
        public string Summary { get; }

        [TextSearchResultValue]
        [VectorStoreRecordData]
        [JsonIgnore]
        public string? Content => $"{Title}\n\n{Summary}";
        public string? Link { get; }
        [TextSearchResultValue]
        [VectorStoreRecordData]
        public string? Source { get; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Id = Guid.CreateVersion7().ToString();
            Title = syndicationItem.Title?.Text ?? string.Empty;
            Summary = syndicationItem.Summary?.Text ?? string.Empty;
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
            Source = string.IsNullOrWhiteSpace(Link) ? null : new Uri(Link).DnsSafeHost;
        }

#pragma warning disable CS8618 // Vector Database requires a parameterless constructor
        public NewsItem() { }
#pragma warning restore CS8618

        public override string ToString()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            return jsonString;
        }
    }
}
