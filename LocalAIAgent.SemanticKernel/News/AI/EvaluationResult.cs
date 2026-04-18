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
        public string? Topic { get; set; }
        public ChatTokenUsage? TokenUsage { get; set; }

        public static List<EvaluationResult> Deserialize(string json)
        {
            // Strip think block emitted by reasoning models (<|think> ... <think|>)
            const string thinkOpen = "<|think>";
            const string thinkClose = "<think|>";
            int thinkStart = json.IndexOf(thinkOpen, StringComparison.Ordinal);
            int thinkEnd = json.IndexOf(thinkClose, StringComparison.Ordinal);
            if (thinkStart >= 0 && thinkEnd > thinkStart)
                json = json[(thinkEnd + thinkClose.Length)..];

            const string channelMarker = "<channel|>";
            int markerIndex = json.IndexOf(channelMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
                json = json[(markerIndex + channelMarker.Length)..];

            // Use LastIndexOf so that any stray '[' in the think block is ignored —
            // the JSON array always appears last in the model's response.
            int arrayStart = json.LastIndexOf('[');
            if (arrayStart < 0)
                throw new InvalidOperationException("No JSON array found in LLM response.");
            int arrayEnd = json.LastIndexOf(']');
            if (arrayEnd < arrayStart)
                throw new InvalidOperationException("No closing ']' found in LLM response.");
            json = json[arrayStart..(arrayEnd + 1)];

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
