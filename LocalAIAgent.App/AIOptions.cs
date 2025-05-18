namespace LocalAIAgent.App
{
    internal class AIOptions
    {
        public required string ModelId { get; set; }
        public required string EndpointUrl { get; set; }
        public string ApiKey { get; set; } = string.Empty;

        public decimal Temperature { get; set; }
        public decimal TopP { get; set; }
        public decimal FrequencyPenalty { get; set; }
        public decimal PresencePenalty { get; set; }
    }
}
