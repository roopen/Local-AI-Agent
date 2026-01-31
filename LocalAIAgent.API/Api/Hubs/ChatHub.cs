using LocalAIAgent.SemanticKernel.News.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;

namespace LocalAIAgent.API.Api.Hubs
{
    public class ChatHub(INewsChatUseCase newsChatUseCase, ChatContextStore chatContextStore) : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message, user, Guid.CreateVersion7());

            List<string> context = chatContextStore.GetContext(Context.ConnectionId);
            if (context is null || context.Count is 0)
            {
                context = [message];
                chatContextStore.SaveContext(Context.ConnectionId, context);
            }

            await foreach (StreamingChatMessageContent content in newsChatUseCase.GetChatStreamAsync(context))
            {
                await Clients.All.SendAsync("ReceiveMessage", content.Content, "AI", Guid.CreateVersion7());
                context.Add(content.Content ?? string.Empty);
                chatContextStore.SaveContext(Context.ConnectionId, context);
            }
        }
    }
}
