using System.Text.RegularExpressions;
using LocalAIAgent.Application.News.Reader;

namespace LocalAIAgent.API.Api.Controllers.Serialization;

internal static class ArticleReaderDiagnostics
{
    internal static string Describe(Exception error)
    {
        string detail = error is ArticleReaderUnavailableException known
            ? known.Diagnostic ?? known.Message
            : $"{error.GetType().Name}: {error.Message}\n{error.InnerException?.GetType().Name}: {error.InnerException?.Message}";
        // Keep error codes, hosts, tool names and call sites, but omit URL credentials/query tokens.
        detail = Regex.Replace(detail, @"https?://[^\s<>""']+", match =>
        {
            if (!Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri)) return "[redacted URL]";
            return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.ToString();
        });
        detail = Regex.Replace(detail, @"(?i)(authorization\s*[:=]\s*|bearer\s+|(?:api[_-]?key|token|password|secret|cookie)\s*[:=]\s*)[^\r\n,;]+", "$1[redacted]");
        return detail[..Math.Min(detail.Length, 2000)].Trim();
    }
}
