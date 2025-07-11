﻿using LocalAIAgent.Domain;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    internal class EvaluationResult
    {
        public required Relevancy Relevancy { get; set; }
        public string? Category { get; set; }
        public string? Reasoning { get; set; }

        public static List<EvaluationResult> Deserialize(string json)
        {
            string cleaned = json
                .Replace("```json", "")
                .Replace("```", "")
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
