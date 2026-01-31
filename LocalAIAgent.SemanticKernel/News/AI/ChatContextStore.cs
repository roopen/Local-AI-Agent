using Microsoft.Extensions.Caching.Memory;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public class ChatContextStore(IMemoryCache memoryCache)
    {
        public void SaveContext(string key, List<string> chatHistory)
        {
            memoryCache.Set(key, chatHistory);
        }

        public List<string> GetContext(string key)
        {
            if (memoryCache.TryGetValue(key, out var value) && value is List<string> chatHistory)
            {
                return chatHistory;
            }
            return [];
        }
    }
}
