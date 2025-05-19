namespace LocalAIAgent.App.Options
{
    internal class EmbeddingOptions
    {
        public required string ModelId { get; set; }
        public required string EndpointUrl { get; set; }
        public string ApiKey { get; set; } = string.Empty;
    }
}
