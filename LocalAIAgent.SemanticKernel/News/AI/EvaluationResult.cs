using LocalAIAgent.Domain;
using OpenAI.Chat;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    internal class EvaluationResult
    {
        public int ArticleIndex { get; set; }
        public required Relevancy Relevancy { get; set; }
        public string? Category { get; set; }
        public string? Reasoning { get; set; }
        public ChatTokenUsage? TokenUsage { get; set; }

        public static List<EvaluationResult> Deserialize(string json)
        {
            string cleaned = json
                .Replace("```json", "")
                .Replace("```", "")
                .Replace("Relavancy", "Relevancy")
                .Trim();

            return System.Text.Json.JsonSerializer.Deserialize<List<EvaluationResult>>(cleaned, options) ??
                   throw new InvalidOperationException("Failed to deserialize EvaluationResult from JSON.");
        }

        private static readonly System.Text.Json.JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }
}
