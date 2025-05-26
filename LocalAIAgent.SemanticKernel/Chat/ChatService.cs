using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Text;

namespace LocalAIAgent.SemanticKernel.Chat
{
    public class ChatService(IChatCompletionService chatCompletion, Kernel kernel, AIOptions options, ChatContext chatContext)
    {
        private const string ChatSystemPrompt = "You are an AI assistant. " +
            "When asked about news, you curate the current news according to user's preferences (prioritize likes and filter away news based on" +
            "dislikes) " +
            "Try to find the most significant news using NewsService. " +
            "Use the following template for news reporting and fill information from the json response: \n" +
            "Category - Title - Summary (Date) [Link] \n The news information will be in json format. " +
            "Keep your answers short, but display a large variety of news. Be willing to discuss any news with the user.\n" +
            "All news info comes as a json string containing Content (create Title, Category and Summary with this), Link and Source information.";

        public async Task StartChat()
        {
            ChatHistory chatHistory = [];

            StringBuilder fullAssistantContent = new();

            OpenAIPromptExecutionSettings openAiSettings = GetOpenAIPromptExecutionSettings(
                options,
                ChatSystemPrompt + "User's dislikes: \n" + chatContext.GetUserDislikesAsString());

            while (true)
            {
                Console.Write("User: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) { break; }
                chatHistory.AddUserMessage(input);
                Console.WriteLine("Assistant: ");

                await foreach (StreamingChatMessageContent? content in chatCompletion.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    openAiSettings,
                    kernel)
                    .ConfigureAwait(false))
                {
                    Console.Write(content.Content);
                    fullAssistantContent.Append(content.Content);
                }

                chatHistory.AddAssistantMessage(fullAssistantContent.ToString());
                Console.WriteLine();
            }
        }

        public async Task<List<string>> GetDislikedTopicsList(string userPrompt)
        {
            string unwantedTopicsPrompt = "Provide a list of unwanted topics as keywords (to be used in filtering news) in the following user prompt. " +
                "Skip if topic cannot be represented as a single keyword." +
                "Keywords are to be separated with commas. <example>keyword1,keyword2,keyword3</example>" +
                "The response should contain nothing other than a plain list of words.\n";

            ChatHistory chatHistory = [];
            chatHistory.AddUserMessage(userPrompt);

            IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent> response = await chatCompletion.GetChatMessageContentsAsync(
                chatHistory,
                GetOpenAIPromptExecutionSettings(options, unwantedTopicsPrompt, allowFunctionUse: false),
                kernel
            );

            List<string> unwantedTopics = response.First().Content!.Split(',').ToList();
            unwantedTopics.RemoveAll(string.IsNullOrWhiteSpace);
            unwantedTopics = unwantedTopics.Select(topic => topic.Trim()).ToList();

            return unwantedTopics;
        }

        public async Task<List<string>> GetInterestingTopicsList(string userPrompt)
        {
            string unwantedTopicsPrompt = "Provide a list of desired topics as keywords (to be used in filtering news) in the following user prompt. " +
                "Skip if topic cannot be represented as a single keyword." +
                "Keywords are to be separated with commas. <example>keyword1,keyword2,keyword3</example>" +
                "The response should contain nothing other than a plain list of words.\n";

            ChatHistory chatHistory = [];
            chatHistory.AddUserMessage(userPrompt);

            IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent> response = await chatCompletion.GetChatMessageContentsAsync(
                chatHistory,
                GetOpenAIPromptExecutionSettings(options, unwantedTopicsPrompt, allowFunctionUse: false),
                kernel
            );

            List<string> wantedTopics = response.First().Content!.Split(',').ToList();
            wantedTopics.RemoveAll(string.IsNullOrWhiteSpace);
            wantedTopics = wantedTopics.Select(topic => topic.Trim()).ToList();

            return wantedTopics;
        }

        private static OpenAIPromptExecutionSettings GetOpenAIPromptExecutionSettings(AIOptions options, string systemPrompt, bool allowFunctionUse = true)
        {
            return new OpenAIPromptExecutionSettings
            {
                ChatSystemPrompt = systemPrompt,
                ReasoningEffort = ChatReasoningEffortLevel.High,
                FunctionChoiceBehavior = allowFunctionUse ? FunctionChoiceBehavior.Auto() : FunctionChoiceBehavior.None(),
                Temperature = (double)options.Temperature,
                TopP = (double)options.TopP,
                FrequencyPenalty = (double)options.FrequencyPenalty,
                PresencePenalty = (double)options.PresencePenalty,
            };
        }
    }
}
