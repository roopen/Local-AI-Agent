namespace LocalAIAgent.SemanticKernel.RAG.Embedding
{
    internal class EmbeddingRequest
    {
        public required string Model { get; set; }
        public required List<string> Input { get; set; }

    }
}