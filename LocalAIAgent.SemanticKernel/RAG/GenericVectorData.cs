using Microsoft.Extensions.VectorData;

namespace LocalAIAgent.SemanticKernel.RAG
{
    internal partial class RAGService
    {
        private sealed class GenericVectorData : BaseVectorData
        {
            [VectorStoreRecordKey]
            public string Key { get; set; } = Guid.CreateVersion7().ToString();

            [VectorStoreRecordData]
            public string Chunk { get; set; }
        }
    }
}
