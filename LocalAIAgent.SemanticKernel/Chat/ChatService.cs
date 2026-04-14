using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalAIAgent.SemanticKernel.Chat
{
    public class ChatService(Kernel kernel, AIOptions options, ChatContext chatContext) : IChatService
    {
        private const string ChatSystemPrompt = "You are an AI assistant. " +
            "When asked about news, you curate the current news according to user's preferences (prioritize likes and filter away news based on" +
            "dislikes) " +
            "Try to find the most significant news using NewsService. " +
            "Use the following template for news reporting and fill information from the json response: \n" +
            "Category - Title - Summary (Date) [Link] \n The news information will be in json format. " +
            "Keep your answers short, but display a large variety of news. Be willing to discuss any news with the user.\n" +
            "All news info comes as a json string containing Content (create Title, Category and Summary with this), Link and Source information.";

        private readonly ChatHistoryAgentThread _thread = new();

        private readonly ChatCompletionAgent _agent = new()
        {
            Name = "ChatAssistant",
            Instructions = ChatSystemPrompt +
                "User's dislikes: \n" + chatContext.GetUserDislikesAsString() + "\n" +
                "User's likes: \n" + chatContext.GetUserInterestsAsString(),
            Kernel = kernel,
            Arguments = new KernelArguments(options.GetAgentExecutionSettings()),
        };

        public ChatHistory chatHistory => _thread.ChatHistory;

        public async Task StartConsoleChat()
        {
            while (true)
            {
                Console.Write("User: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) { break; }
                Console.WriteLine("Assistant: ");

                await foreach (StreamingChatMessageContent content in GetChatStreamAsync(input).ConfigureAwait(false))
                {
                    Console.Write(content.Content);
                }

                Console.WriteLine();
            }
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetChatStreamAsync(string userMessage)
        {
            ChatMessageContent message = new(AuthorRole.User, userMessage);
            await foreach (StreamingChatMessageContent content in _agent.InvokeStreamingAsync(message, _thread).ConfigureAwait(false))
            {
                yield return content;
            }
        }

        public async Task<List<string>> GetDislikedTopicsList(string userPrompt)
        {
            const string systemPrompt = "Provide a list of unwanted topics as keywords (to be used in filtering news) in the following user prompt. " +
                "Skip if topic cannot be represented as a single keyword." +
                "Keywords are to be separated with commas. <example>keyword1,keyword2,keyword3</example>" +
                "The response should contain nothing other than a plain list of words.\n";

            ChatCompletionAgent topicsAgent = new()
            {
                Instructions = systemPrompt,
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false)),
            };

            ChatHistoryAgentThread thread = new();
            ChatMessageContent message = new(AuthorRole.User, userPrompt);

            string content = string.Empty;
            await foreach (StreamingChatMessageContent chunk in topicsAgent.InvokeStreamingAsync(message, thread).ConfigureAwait(false))
                content += chunk.Content ?? string.Empty;

            List<string> unwantedTopics = content.Split(',').ToList();
            unwantedTopics.RemoveAll(string.IsNullOrWhiteSpace);
            unwantedTopics = unwantedTopics.Select(topic => topic.Trim()).ToList();

            return unwantedTopics;
        }

        public async Task<List<string>> GetInterestingTopicsList(string userPrompt)
        {
            const string systemPrompt = "Provide a list of desired topics as keywords (to be used in filtering news) in the following user prompt. " +
                "Skip if topic cannot be represented as a single keyword." +
                "Keywords are to be separated with commas. <example>keyword1,keyword2,keyword3</example>" +
                "The response should contain nothing other than a plain list of words.\n";

            ChatCompletionAgent topicsAgent = new()
            {
                Instructions = systemPrompt,
                Kernel = kernel,
                Arguments = new KernelArguments(options.GetAgentExecutionSettings(allowFunctionUse: false)),
            };

            ChatHistoryAgentThread thread = new();
            ChatMessageContent message = new(AuthorRole.User, userPrompt);

            string content = string.Empty;
            await foreach (StreamingChatMessageContent chunk in topicsAgent.InvokeStreamingAsync(message, thread).ConfigureAwait(false))
                content += chunk.Content ?? string.Empty;

            List<string> wantedTopics = content.Split(',').ToList();
            wantedTopics.RemoveAll(string.IsNullOrWhiteSpace);
            wantedTopics = wantedTopics.Select(topic => topic.Trim()).ToList();

            return wantedTopics;
        }
    }
}
