using Microsoft.Extensions.AI;

namespace LocalAIAgent.SemanticKernel.Chat
{
    public class AIOptions
    {
        public required string ModelId { get; set; }
        public required string LanguageModelId { get; set; }
        public required string EndpointUrl { get; set; }
        public string ApiKey { get; set; } = string.Empty;

        public bool UseResultsForDataset { get; set; }

        public decimal Temperature { get; set; }
        public decimal TopP { get; set; }
        public decimal FrequencyPenalty { get; set; }
        public decimal PresencePenalty { get; set; }

        public ChatOptions BuildChatOptions(ChatResponseFormat? responseFormat = null)
        {
            return new ChatOptions
            {
                Temperature = (float)Temperature,
                TopP = (float)TopP,
                FrequencyPenalty = (float)FrequencyPenalty,
                PresencePenalty = (float)PresencePenalty,
                ResponseFormat = responseFormat,
            };
        }
    }
}
