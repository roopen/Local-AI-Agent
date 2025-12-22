using LocalAIAgent.SemanticKernel.RAG;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.News
{
    public partial class NewsItem : BaseVectorData
    {
        [VectorStoreKey]
        [JsonIgnore]
        public string Id { get; }
        public DateTimeOffset PublishDate { get; }
        public string Title { get; }
        public string Summary { get; }
        public List<string> Categories { get; } = [];

        [TextSearchResultValue]
        [VectorStoreData]
        [JsonIgnore]
        public string? Content => $"{Title}\n\n{Summary}";
        public string? Link { get; }
        [TextSearchResultValue]
        [VectorStoreData]
        public string? Source { get; }

        public NewsItem(SyndicationItem syndicationItem)
        {
            Id = Guid.CreateVersion7().ToString();
            Title = GetDecodedHtmlString(syndicationItem.Title?.Text);
            Summary = GetDecodedHtmlString(syndicationItem.Summary?.Text);
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
            Source = string.IsNullOrWhiteSpace(Link) ? null : new Uri(Link).DnsSafeHost;
            if (syndicationItem.Categories is not null)
            {
                Categories = syndicationItem.Categories.Select(c => c.Name ?? c.Label).ToList();
            }
        }

        private static string GetDecodedHtmlString(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string decodedText = WebUtility.HtmlDecode(text) ?? string.Empty;

            // Remove HTML tags if any
            decodedText = HtmlTag().Replace(decodedText, string.Empty);

            return decodedText;
        }

#pragma warning disable CS8618 // Vector Database requires a parameterless constructor
        public NewsItem() { }
#pragma warning restore CS8618

        public override string ToString()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            return jsonString;
        }

        [System.Text.RegularExpressions.GeneratedRegex("<.*?>")]
        private static partial System.Text.RegularExpressions.Regex HtmlTag();
    }
}
