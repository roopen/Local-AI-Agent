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

        /// <summary>The feed URL (only populated for custom feeds).</summary>
        public string? Url { get; init; }

        /// <summary>If the last fetch attempt failed, surfaces the error to the UI.</summary>
        public string? LastFetchErrorMessage { get; init; }
    }

    public sealed record ToggleFeedDto
    {
        public required int UserId { get; init; }
        public required string ClientName { get; init; }
        public required bool Enabled { get; init; }
    }

    public sealed record AddCustomFeedDto
    {
        public required int UserId { get; init; }
        public required string Url { get; init; }
        public required string DisplayName { get; init; }
        public required string Language { get; init; }
    }
}
