using Vision.SecurityOperationsService.Application.Assets.Queries;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

// Nested asset summary for incident context
public sealed record IncidentAssetDto(Guid Id, string Name, string AssetType);

// Incident list item — returned from GET /api/v1/incidents
public sealed record IncidentListItemDto(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    IncidentAssetDto? Asset,
    LocationDto Location,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    Guid? WorkOrderId);

// Nested asset detail for incident detail view
public sealed record IncidentAssetDetailDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string AssetType,
    string Status);

// Full incident detail — returned from GET /api/v1/incidents/{id}
public sealed record IncidentDetailDto(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    string? ResolutionSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    Guid? WorkOrderId,
    IncidentAssetDetailDto? Asset,
    LocationDto Location,
    BuildingDto Building);
