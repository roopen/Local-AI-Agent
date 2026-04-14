using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalAIAgent.SemanticKernel.Chat
{
    public interface IChatService
    {
        ChatHistory chatHistory { get; }
        IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage);
        Task<List<string>> GetDislikedTopicsList(string userPrompt);
        Task<List<string>> GetInterestingTopicsList(string userPrompt);
        Task StartConsoleChat();
    }
}