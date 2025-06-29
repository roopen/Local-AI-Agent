using Microsoft.Extensions.VectorData;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.RAG
{
    public abstract class BaseVectorData
    {
        [NotMapped]
        [JsonIgnore]
        public ReadOnlyMemory<float> Vector { get; set; }

        [VectorStoreRecordVector(768, DistanceFunction = DistanceFunction.CosineDistance)]
        [JsonIgnore]
        public ReadOnlyMemory<float> DifferenceVector { get { return Vector; } }

        [VectorStoreRecordVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
        [JsonIgnore]
        public ReadOnlyMemory<float> SimilarityVector { get { return Vector; } }
    }
}