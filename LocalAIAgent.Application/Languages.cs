using System.Collections.Frozen;
using System.Globalization;

namespace LocalAIAgent.Application
{
    /// <summary>
    /// BCP-47 / ISO 639-1 language helpers backed by .NET's <see cref="CultureInfo"/>.
    /// Validation rejects anything not in the predefined-culture list — <c>CultureInfo.GetCultureInfo()</c>
    /// alone is too permissive (it manufactures a custom culture from arbitrary input).
    /// </summary>
    public static class Languages
    {
        public const string DefaultTargetLanguage = "en";

        /// <summary>Specific cultures we treat as first-class targets in addition to the neutral list.</summary>
        private static readonly FrozenSet<string> IncludedSpecificCultures = new[]
        {
            "zh-TW", "zh-CN", "pt-BR", "pt-PT",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lazily-built lookup of all codes we treat as supported (case-insensitive).</summary>
        private static readonly FrozenSet<string> SupportedCodes = BuildSupportedCodes();

        private static FrozenSet<string> BuildSupportedCodes()
        {
            HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);
            foreach (CultureInfo c in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            {
                if (!c.Equals(CultureInfo.InvariantCulture)) codes.Add(c.Name);
            }
            foreach (string code in IncludedSpecificCultures) codes.Add(code);
            return codes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>True when the code is a known ISO neutral culture or a curated specific culture.</summary>
        public static bool IsSupported(string code) =>
            !string.IsNullOrWhiteSpace(code) && SupportedCodes.Contains(code);

        /// <summary>
        /// Returns the English display name for a supported code, or the code itself if it isn't recognized.
        /// </summary>
        public static string GetDisplayName(string code)
        {
            if (!IsSupported(code)) return code;
            return CultureInfo.GetCultureInfo(code).EnglishName;
        }

        /// <summary>
        /// All supported languages — neutral cultures plus the curated specifics — sorted by English name.
        /// </summary>
        public static IReadOnlyList<(string Code, string Name)> GetAllSupported()
        {
            return [.. SupportedCodes
                .Select(code => (Code: code, Name: CultureInfo.GetCultureInfo(code).EnglishName))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        }
    }
}
