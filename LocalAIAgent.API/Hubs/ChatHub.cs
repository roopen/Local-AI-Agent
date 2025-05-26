using LocalAIAgent.SemanticKernel.Chat;
using LocalAIAgent.SemanticKernel.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace LocalAIAgent.API.Hubs
{
    public class ChatHub : Hub
    {
        private readonly Kernel _kernel;
        private readonly ChatService _chatService;

        public ChatHub(Kernel kernel)
        {
            _kernel = kernel;
            _chatService = new(
                kernel.Services.GetService<IChatCompletionService>()!,
                kernel,
                kernel.Services.GetService<AIOptions>()!,
                kernel.Services.GetService<ChatContext>()!
            );
            _ = kernel.InitializeVectorDatabase();
        }

        public async Task SendMessage(string user, string message)
        {
            StringBuilder stringBuilder = new();
            Console.WriteLine(message);
            _chatService.chatHistory.AddUserMessage(message);

            await Clients.All.SendAsync("ReceiveMessage", message, user, Guid.CreateVersion7());

            await foreach (StreamingChatMessageContent content in _chatService.GetChatStreamAsync())
            {
                await Clients.All.SendAsync("ReceiveMessage", content.Content, "AI", Guid.CreateVersion7());
            }
        }
    }
}
