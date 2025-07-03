﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace LocalAIAgent.SemanticKernel.Chat
{
    public class AIOptions
    {
        public required string ModelId { get; set; }
        public required string EndpointUrl { get; set; }
        public string ApiKey { get; set; } = string.Empty;

        public decimal Temperature { get; set; }
        public decimal TopP { get; set; }
        public decimal FrequencyPenalty { get; set; }
        public decimal PresencePenalty { get; set; }

        public OpenAIPromptExecutionSettings GetOpenAIPromptExecutionSettings(
            string systemPrompt,
            bool allowFunctionUse = true)
        {
            return new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = systemPrompt,
                ReasoningEffort = ChatReasoningEffortLevel.High,
                FunctionChoiceBehavior = allowFunctionUse ? FunctionChoiceBehavior.Auto() : FunctionChoiceBehavior.None(),
                Temperature = (double)Temperature,
                TopP = (double)TopP,
                FrequencyPenalty = (double)FrequencyPenalty,
                PresencePenalty = (double)PresencePenalty,
            };
        }
    }
}
