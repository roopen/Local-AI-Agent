namespace LocalAIAgent.API.Infrastructure.Models
{
    public class AiSettings
    {
        public int Id { get; set; }
        public required string ModelId { get; set; } = "gemma-3-27b-it-qat";
        public string ApiKey { get; set; } = string.Empty;
        public required string EndpointUrl { get; set; } = "http://localhost:1234/v1/";
        public decimal Temperature { get; set; } = 0.2m;
        public decimal TopP { get; set; } = 1.0m;
        public decimal FrequencyPenalty { get; set; } = 1.0m;
        public decimal PresencePenalty { get; set; } = 1.0m;
        public int UserPreferencesId { get; set; }

        public required virtual UserPreferences UserPreferences { get; set; }
    }
}