namespace LocalAIAgent.API.Api.Controllers.Serialization
{
    public record AiSettingsDto
    {
        public int UserId { get; set; }
        public required string ModelId { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public required string EndpointUrl { get; set; }
        public decimal Temperature { get; set; }
        public decimal TopP { get; set; }
        public decimal FrequencyPenalty { get; set; }
        public decimal PresencePenalty { get; set; }
    }
}