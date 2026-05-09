namespace LocalAIAgent.Application
{
    /// <summary>
    /// Supported BCP-47 / ISO 639-1 language codes mapped to their English display names.
    /// Used by feed metadata (each feed declares its source language) and user preferences
    /// (each user picks a target language for translation).
    /// </summary>
    public static class Languages
    {
        public const string DefaultTargetLanguage = "en";

        // Order is preserved for UI dropdowns. English first, then alphabetical.
        public static readonly IReadOnlyDictionary<string, string> CodeToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["de"] = "German",
            ["el"] = "Greek",
            ["es"] = "Spanish",
            ["et"] = "Estonian",
            ["fi"] = "Finnish",
            ["fr"] = "French",
            ["it"] = "Italian",
            ["ja"] = "Japanese",
            ["no"] = "Norwegian",
            ["ru"] = "Russian",
            ["sv"] = "Swedish",
            ["zh-TW"] = "Traditional Chinese",
        };

        public static string GetDisplayName(string code) =>
            CodeToName.TryGetValue(code, out string? name) ? name : code;

        public static bool IsSupported(string code) => CodeToName.ContainsKey(code);
    }
}
