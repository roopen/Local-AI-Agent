using System.ComponentModel.DataAnnotations;

namespace LocalAIAgent.API.Api.Controllers.Serialization;

public sealed record AiSettingsCatalogResponse
{
    public bool IsConfigured { get; init; }
    public bool IsOwner { get; init; }
    public int? SelectedSettingsId { get; init; }
    public required List<AiSettingsOptionResponse> Options { get; init; }
}

public sealed record AiSettingsOptionResponse
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string ModelId { get; init; }
    public required string EndpointUrl { get; init; }
    public bool HasApiKey { get; init; }
    public bool IsAvailable { get; init; }
    public decimal Temperature { get; init; }
    public decimal TopP { get; init; }
    public decimal FrequencyPenalty { get; init; }
    public decimal PresencePenalty { get; init; }
}

public sealed record SaveAiSettingsOptionRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public required string Name { get; init; }

    [Required]
    public required string ModelId { get; init; }

    [Required]
    public required string EndpointUrl { get; init; }

    public string? ApiKey { get; init; }
    public bool ClearApiKey { get; init; }

    [Range(0, 2)]
    public decimal Temperature { get; init; }

    [Range(0, 1)]
    public decimal TopP { get; init; }

    [Range(-2, 2)]
    public decimal FrequencyPenalty { get; init; }

    [Range(-2, 2)]
    public decimal PresencePenalty { get; init; }
}

public sealed record SelectAiSettingsRequest
{
    [Range(1, int.MaxValue)]
    public int SettingsId { get; init; }
}
