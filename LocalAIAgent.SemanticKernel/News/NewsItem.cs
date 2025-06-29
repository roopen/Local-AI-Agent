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

        [TextSearchResultValue]
        [VectorStoreRecordData]
        public string? Content { get; }
        public string? Link { get; }
        [TextSearchResultValue]
        [VectorStoreRecordData]
        public string? Source { get; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Id = Guid.CreateVersion7().ToString();
            Content = syndicationItem.Title?.Text + "\n" + syndicationItem.Summary?.Text;
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
