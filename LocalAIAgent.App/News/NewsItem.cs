using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using System.ServiceModel.Syndication;

namespace LocalAIAgent.App.News
{
    public class NewsItem
    {
        [VectorStoreRecordKey]
        public string Id { get; }
        public DateTimeOffset PublishDate { get; }

        [TextSearchResultValue]
        [VectorStoreRecordData]
        public string? Content { get; }
        public string? Link { get; }
        [TextSearchResultValue]
        [VectorStoreRecordData]
        public string? Source { get; }
        /// <summary>
        /// The text embedding for this snippet. This is used to search the vector store.
        /// While this is a string property it has the vector attribute, which means whatever
        /// text it contains will be converted to a vector and stored as a vector in the vector store.
        /// </summary>
        public float[] Embedding { get; set; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Id = Guid.NewGuid().ToString();
            Content = syndicationItem.Title?.Text + syndicationItem.Summary?.Text;
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
