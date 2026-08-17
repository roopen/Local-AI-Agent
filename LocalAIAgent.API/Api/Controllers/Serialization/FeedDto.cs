namespace LocalAIAgent.API.Api.Controllers.Serialization
{
    public sealed record FeedDto
    {
        public required string ClientName { get; init; }
        public required string DisplayName { get; init; }
        public required string Language { get; init; }
        public required string LanguageName { get; init; }
        public required bool Enabled { get; init; }

        /// <summary>True for user-added feeds; false for built-in sources.</summary>
        public required bool IsCustom { get; init; }

        /// <summary>The DB id of the custom feed (null for built-ins). Used by the delete endpoint.</summary>
        public int? CustomFeedId { get; init; }

        /// <summary>The feed URLs (only populated for custom feeds — a single feed entry can hold multiple URLs).</summary>
        public IReadOnlyList<string>? Urls { get; init; }

        /// <summary>If the last fetch attempt failed, surfaces the error to the UI.</summary>
        public string? LastFetchErrorMessage { get; init; }
    }

    public sealed record ToggleFeedDto
    {
        public required string ClientName { get; init; }
        public required bool Enabled { get; init; }
    }

    public sealed record AddCustomFeedDto
    {
        public required IReadOnlyList<string> Urls { get; init; }
        public required string DisplayName { get; init; }
        public required string Language { get; init; }
    }

    /// <summary>Detailed result from <c>POST /api/Feeds/Custom</c> when validation fails.</summary>
    public sealed record AddCustomFeedErrorDto
    {
        public required string Message { get; init; }

        /// <summary>Per-URL error map; key is the offending URL, value is the failure reason.</summary>
        public IReadOnlyDictionary<string, string>? UrlErrors { get; init; }
    }

    /// <summary>One entry from <c>GET /api/Feeds/Languages</c>.</summary>
    public sealed record LanguageOptionDto
    {
        /// <summary>BCP-47 / ISO 639-1 code (e.g. <c>"en"</c>, <c>"zh-TW"</c>).</summary>
        public required string Code { get; init; }

        /// <summary>English display name (e.g. <c>"English"</c>, <c>"Chinese (Traditional)"</c>).</summary>
        public required string Name { get; init; }
    }
}

