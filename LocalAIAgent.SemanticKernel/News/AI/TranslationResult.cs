namespace LocalAIAgent.SemanticKernel.News.AI
{
    internal class TranslationResult
    {
        public required string TranslatedTitle { get; set; }
        public required string TranslatedSummary { get; set; }

        public static TranslationResult Deserialize(string json)
        {
            string cleaned = json
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            return System.Text.Json.JsonSerializer.Deserialize<TranslationResult>(cleaned, options) ??
                   throw new InvalidOperationException("Failed to deserialize EvaluationResult from JSON.");
        }

        private static readonly System.Text.Json.JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }
}
