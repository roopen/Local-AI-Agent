using Microsoft.Extensions.Logging;

namespace LocalAIAgent.SemanticKernel.News
{
    public static class NewsLogging
    {
        internal static readonly Action<ILogger, int, int, double, Exception?> LogNewsFiltered =
            LoggerMessage.Define<int, int, double>(
                LogLevel.Information,
                new EventId(1, nameof(LogNewsFiltered)),
                "NewsService: Filtered news articles. Total: {TotalCount}, After filtering: {FilteredCount}. Filter percentage: {FilterPercentage:F1}%");

        internal static readonly Action<ILogger, int, int, int, Exception?> LogTokenUsage =
            LoggerMessage.Define<int, int, int>(
                LogLevel.Information,
                new EventId(2, nameof(LogTokenUsage)),
                "NewsService: Batch token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}");
    }
}
