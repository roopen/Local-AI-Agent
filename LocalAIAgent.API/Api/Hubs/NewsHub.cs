using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Domain;
using LocalAIAgent.SemanticKernel.News;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace LocalAIAgent.API.Api.Hubs
{
    public class NewsHub(
        IGetNewsUseCase getNewsUseCase,
        IGetUserUseCase getUserUseCase,
        NewsMetrics newsMetrics) : Hub
    {
        private static readonly ConcurrentDictionary<string, string> UserConnections = new();

        public override async Task OnConnectedAsync()
        {
            string? userId = Context.UserIdentifier;

            if (userId != null)
            {
                if (UserConnections.TryGetValue(userId, out string? oldConnectionId))
                {
                    // Optionally notify or disconnect the old connection
                    await Clients.Client(oldConnectionId).SendAsync("ForceDisconnect");
                }

                UserConnections[userId] = Context.ConnectionId;
            }

            await base.OnConnectedAsync();
        }

        public async IAsyncEnumerable<NewsArticle> GetNewsStream(
            int userId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            newsMetrics.StartRecordingRequest();

            User? user = await getUserUseCase.GetUserById(userId)
                ?? throw new HubException($"User with ID {userId} not found.");

            if (user.Preferences is null)
                throw new HubException("User preferences are not set.");

            int newsCount = 0;
            await foreach (NewsArticle? newsArticle in getNewsUseCase.GetNewsStreamAsync(user.Preferences, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                if (newsArticle is not null)
                {
                    newsCount++;
                    yield return newsArticle;
                }
            }

            newsMetrics.RecordNewsArticleCount(newsCount);
            newsMetrics.StopRecordingRequest();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            string? userId = Context.UserIdentifier;
            if (userId != null)
            {
                UserConnections.TryRemove(userId, out _);
            }

            return base.OnDisconnectedAsync(exception);
        }
    }
}
