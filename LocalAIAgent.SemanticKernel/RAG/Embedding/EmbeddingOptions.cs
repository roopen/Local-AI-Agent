namespace LocalAIAgent.SemanticKernel.RAG.Embedding
{
    internal class EmbeddingOptions
    {
        public required string ModelId { get; set; }
        public required string EndpointUrl { get; set; }
        public string ApiKey { get; set; } = string.Empty;
    }
}
