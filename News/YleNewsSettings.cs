namespace Local_AI_Agent.News
{
    internal class YleNewsSettings
    {
        public static string ClientName { get; } = "YleNewsClient";
        public string UserAgent { get; } = "Local-AI-Agent";
        public string BaseUrl { get; } = "https://yle.fi/";

        public static string MainHeadlinesUrl { get; } = "/rss/uutiset/paauutiset";
        public static string FinanceNewsUrl { get; } = "rss/t/18-19274/fi";
        public static string WorldNewsUrl { get; } = "/rss/t/18-34953/fi";
        public static string FinlandNewsUrl { get; } = "/rss/t/18-34837/fi";

        public static List<string> GetYleNewsUrls()
        {
            return
            [
                MainHeadlinesUrl,
                FinanceNewsUrl,
                WorldNewsUrl,
                FinlandNewsUrl
            ];
        }
    }
}
