
using System.Text.Json;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public class ExpandedNewsResult
    {
        public bool ArticleWasTranslated { get; set; }
        public string? Translation { get; set; }
        public List<KeyValuePair<TermString, ExplanationString>> TermsAndExplanations { get; set; } = [];

        public static string GetJsonSchema()
        {
            return """
            {
                "type": "object",
                "properties": {
                    "ArticleWasTranslated": { "type": "boolean" },
                    "Translation": { "type": ["string", "null"] },
                    "TermsAndExplanations": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "Key": {
                                    "type": "object",
                                    "properties": {
                                        "Term": { "type": "string" }
                                    },
                                    "required": ["Name"]
                                },
                                "Value": {
                                    "type": "object",
                                    "properties": {
                                        "Explanation": { "type": "string" }
                                    },
                                    "required": ["Description"]
                                }
                            },
                            "required": ["Key", "Value"]
                        }
                    }
                },
                "required": ["ArticleWasTranslated", "Translation", "TermsAndExplanations"]
            }
            """;
        }

        internal static ExpandedNewsResult FromJson(string? content)
        {
            if (content is null) return new();

            // The content might contain extra text before or after the JSON object
            var json = ExtractFirstJsonObject(content);

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances

            return JsonSerializer.Deserialize<ExpandedNewsResult>(json ?? string.Empty, options)
                ?? new ExpandedNewsResult();
        }

        private static string? ExtractFirstJsonObject(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }
                else if (c == '}')
                {
                    if (depth > 0)
                    {
                        depth--;

                        if (depth == 0 && start >= 0)
                        {
                            // Found a complete top-level object
                            return input.Substring(start, i - start + 1);
                        }
                    }
                }
            }

            return null;
        }
    }

    public class TermString
    {
        public string Term { get; set; } = string.Empty;
    }

    public class ExplanationString
    {
        public string Explanation { get; set; } = string.Empty;
    }
}