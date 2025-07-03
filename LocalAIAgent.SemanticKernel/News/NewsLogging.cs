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
    }
}
