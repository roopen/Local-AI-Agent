using Microsoft.Extensions.AI;

namespace LocalAIAgent.Application.Chat
{
    public sealed class AIOptions
    {
        public required string ModelId { get; init; }
        public required string EndpointUrl { get; init; }
        public string ApiKey { get; init; } = string.Empty;

        public bool UseResultsForDataset { get; init; }

        public decimal Temperature { get; init; }
        public decimal TopP { get; init; }
        public decimal FrequencyPenalty { get; init; }
        public decimal PresencePenalty { get; init; }

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
