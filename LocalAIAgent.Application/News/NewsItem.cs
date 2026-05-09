using System.Net;
using System.ServiceModel.Syndication;
using System.Text.Json.Serialization;

namespace LocalAIAgent.Application.News
{
    public partial class NewsItem
    {
        public DateTimeOffset PublishDate { get; }
        public string Title { get; }
        public string Summary { get; }
        public List<string> Categories { get; } = [];

        [JsonIgnore]
        public string? Content => $"{Title}\n\n{Summary}";
        public string? Link { get; }
        public string? Source { get; }

        /// <summary>
        /// The <see cref="BaseNewsClientSettings.ClientName"/> of the source that produced this item,
        /// or <c>"custom:{id}"</c> for items from a user's custom feed. Used by
        /// <c>NewsService.FilterNews</c> to honor the user's disabled-feeds list.
        /// Null for items constructed before source attribution was added.
        /// </summary>
        [JsonIgnore]
        public string? SourceClientName { get; }

        /// <summary>
        /// BCP-47 / ISO 639-1 code of the language this article was published in.
        /// Drives the per-article translation decision (translate when this differs from
        /// the user's target language). Null for legacy items without language attribution.
        /// </summary>
        [JsonIgnore]
        public string? Language { get; }

        public NewsItem(SyndicationItem syndicationItem) : this(syndicationItem, null, null) { }

        public NewsItem(SyndicationItem syndicationItem, string? sourceClientName)
            : this(syndicationItem, sourceClientName, null) { }

        public NewsItem(SyndicationItem syndicationItem, string? sourceClientName, string? language)
        {
            Title = GetDecodedHtmlString(syndicationItem.Title?.Text);
            Summary = GetDecodedHtmlString(syndicationItem.Summary?.Text);
            PublishDate = syndicationItem.PublishDate;
            Link = syndicationItem.Links.FirstOrDefault()?.Uri.ToString();
            Source = string.IsNullOrWhiteSpace(Link) ? null : new Uri(Link).DnsSafeHost;
            SourceClientName = sourceClientName;
            Language = language;
            if (syndicationItem.Categories is not null)
            {
                Categories = syndicationItem.Categories.Select(c => c.Name ?? c.Label).ToList();
            }
        }

        internal static string GetDecodedHtmlString(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string decodedText = WebUtility.HtmlDecode(text) ?? string.Empty;

            // Remove HTML tags if any
            decodedText = HtmlTag().Replace(decodedText, string.Empty);

            return decodedText;
        }

        public override string ToString()
        {
            string jsonString = System.Text.Json.JsonSerializer.Serialize(this);
            return jsonString;
        }

        [System.Text.RegularExpressions.GeneratedRegex("<.*?>")]
        private static partial System.Text.RegularExpressions.Regex HtmlTag();
    }
}
