using System.Text.Json.Serialization;

namespace Vision.SecurityOperationsService.Infrastructure.Messaging;

/// <summary>
/// Explicit integration event DTO for vision.security-operations.incident-created.v1.
/// Do not serialize domain entities directly into SQS.
/// </summary>
public sealed class IncidentCreatedV1
{
    public const string EventTypeName = "vision.security-operations.incident-created.v1";

    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = EventTypeName;

    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; init; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("incident")]
    public IncidentCreatedIncidentV1 Incident { get; init; } = null!;

    [JsonPropertyName("asset")]
    public IncidentCreatedAssetV1 Asset { get; init; } = null!;

    [JsonPropertyName("location")]
    public IncidentCreatedLocationV1 Location { get; init; } = null!;
}

public sealed class IncidentCreatedIncidentV1
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;
}

public sealed class IncidentCreatedAssetV1
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("assetTag")]
    public string? AssetTag { get; init; }

    [JsonPropertyName("assetType")]
    public string AssetType { get; init; } = string.Empty;
}

public sealed class IncidentCreatedLocationV1
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("buildingId")]
    public Guid BuildingId { get; init; }

    [JsonPropertyName("buildingName")]
    public string BuildingName { get; init; } = string.Empty;
}
