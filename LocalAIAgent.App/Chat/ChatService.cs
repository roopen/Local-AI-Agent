using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using System.Text;

namespace LocalAIAgent.App.Chat
{
    internal class ChatService(IChatCompletionService chatCompletion, Kernel kernel, AIOptions options)
    {
        private const string ChatSystemPrompt = "You are an AI assistant. " +
            "When asked about news, you curate the current news according to user's preferences " +
            "Try to find the most significant news. " +
            "Use the following template for news reporting and fill information from the json response: \n" +
            "**Category:** Title - Summary (Source) [Link] \n The news information will be in json format. " +
            "Keep your answers short. Be willing to discuss any news with the user.\n";

        public async Task StartChat(string userPreferencesPrompt)
        {
            ChatHistory chatHistory = [];

            StringBuilder fullAssistantContent = new();

            OpenAIPromptExecutionSettings openAiSettings = GetOpenAIPromptExecutionSettings(options, ChatSystemPrompt + userPreferencesPrompt);

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

        public async Task<List<string>> GetUnwantedTopics(string userPrompt)
        {
            string unwantedTopicsPrompt = "Provide a list of unwanted topics in the following user prompt. The response must follow following format:" +
                "-Topic\n-Topic\n-Topic\n The response should contain nothing other than a plain list of words.\n";

            ChatHistory chatHistory = [];
            chatHistory.AddUserMessage(userPrompt);

            IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent> response = await chatCompletion.GetChatMessageContentsAsync(
                chatHistory,
                GetOpenAIPromptExecutionSettings(options, unwantedTopicsPrompt, allowFunctionUse: false),
                kernel
            );

            List<string> unwantedTopics = response.First().Content!.Split('-').ToList();
            unwantedTopics.RemoveAll(string.IsNullOrWhiteSpace);
            unwantedTopics = unwantedTopics.Select(topic => topic.Trim()).ToList();

            return unwantedTopics;
        }

        private OpenAIPromptExecutionSettings GetOpenAIPromptExecutionSettings(AIOptions options, string systemPrompt, bool allowFunctionUse = true)
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
