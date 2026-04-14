using System.Text.Json.Serialization;

namespace LocalAIAgent.SemanticKernel.News.AI;

public record LMStudioQuantization(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("bits_per_weight")] double BitsPerWeight);

public record LMStudioCapabilities(
    [property: JsonPropertyName("vision")] bool Vision,
    [property: JsonPropertyName("trained_for_tool_use")] bool TrainedForToolUse);

public record LMStudioModel(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("publisher")] string? Publisher,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("architecture")] string? Architecture,
    [property: JsonPropertyName("quantization")] LMStudioQuantization? Quantization,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("params_string")] string? ParamsString,
    [property: JsonPropertyName("max_context_length")] int MaxContextLength,
    [property: JsonPropertyName("format")] string? Format,
    [property: JsonPropertyName("capabilities")] LMStudioCapabilities? Capabilities,
    [property: JsonPropertyName("description")] string? Description);
