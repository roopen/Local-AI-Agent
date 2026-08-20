using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Domain;
using LocalAIAgent.Application.News;
using LocalAIAgent.Application.Chat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace LocalAIAgent.API.Api.Hubs
{
    [Authorize]
    public class NewsHub(
        IGetNewsUseCase getNewsUseCase,
        IGetUserUseCase getUserUseCase,
        NewsMetrics newsMetrics,
        ILogger<NewsHub> logger) : Hub
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
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            newsMetrics.StartRecordingRequest();

            if (!int.TryParse(Context.UserIdentifier, out int userId))
                throw new HubException("User is not authenticated.");

            User? user = await getUserUseCase.GetUserById(userId)
                ?? throw new HubException($"User with ID {userId} not found.");

            if (user.Preferences is null)
                throw new HubException("User preferences are not set.");

            await SendLoadingPhaseAsync(NewsLoadingPhase.Feeds, cancellationToken);

            int newsCount = 0;
            await using IAsyncEnumerator<NewsArticle> enumerator = getNewsUseCase
                .GetNewsStreamAsync(user.Preferences, cancellationToken, SendLoadingPhaseAsync)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    newsMetrics.StopRecordingRequest();
                    yield break;
                }
                catch (Exception ex)
                {
                    newsMetrics.StopRecordingRequest();
                    string safeMessage = LlmErrorSanitizer.GetSafeMessage(ex);
                    bool isLlmConnectionFailure = LlmErrorSanitizer.IsLlmConnectionFailure(ex);
                    logger.LogError(
                        ex,
                        "News stream failed for user {UserId}: {ErrorType}: {Message}",
                        userId,
                        ex.GetType().Name,
                        safeMessage);
                    throw new HubException(isLlmConnectionFailure
                        ? $"{LlmErrorSanitizer.ConnectionFailureCode}{safeMessage}"
                        : $"Unable to load articles: {safeMessage}");
                }

                if (!hasNext)
                    break;

                NewsArticle newsArticle = enumerator.Current;
                if (newsArticle is not null)
                {
                    newsCount++;
                    yield return newsArticle;
                }
            }

            newsMetrics.RecordNewsArticleCount(newsCount);
            newsMetrics.StopRecordingRequest();
        }

        private Task SendLoadingPhaseAsync(NewsLoadingPhase phase, CancellationToken cancellationToken)
        {
            string clientPhase = phase switch
            {
                NewsLoadingPhase.Feeds => "feeds",
                NewsLoadingPhase.Llm => "llm",
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
            };

            return Clients.Caller.SendAsync("NewsLoadingPhaseChanged", clientPhase, cancellationToken);
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
