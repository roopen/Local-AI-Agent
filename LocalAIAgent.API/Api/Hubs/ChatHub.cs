using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.API.Api.Hubs
{
    public class ChatHub(INewsChatUseCase newsChatUseCase) : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message, user, Guid.CreateVersion7());

            await foreach (StreamingChatMessageContent content in newsChatUseCase.GetChatStreamAsync(message))
            {
                await Clients.All.SendAsync("ReceiveMessage", content.Content, "AI", Guid.CreateVersion7());
            }
        }
    }
}
