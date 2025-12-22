using Microsoft.Extensions.VectorData;

namespace LocalAIAgent.SemanticKernel.RAG
{
    internal partial class RAGService
    {
        private sealed class GenericVectorData : BaseVectorData
        {
            [VectorStoreKey]
            public string Key { get; set; } = Guid.CreateVersion7().ToString();

            [VectorStoreData]
            public string? Chunk { get; set; }
        }
    }
}
