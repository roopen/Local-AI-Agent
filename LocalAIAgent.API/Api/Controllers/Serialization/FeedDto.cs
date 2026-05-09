namespace LocalAIAgent.API.Api.Controllers.Serialization
{
    public sealed record FeedDto
    {
        public required string ClientName { get; init; }
        public required string DisplayName { get; init; }
        public required string Language { get; init; }
        public required string LanguageName { get; init; }
        public required bool Enabled { get; init; }
    }

    public sealed record ToggleFeedDto
    {
        public required int UserId { get; init; }
        public required string ClientName { get; init; }
        public required bool Enabled { get; init; }
    }
}
