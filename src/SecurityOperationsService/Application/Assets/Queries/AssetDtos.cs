namespace Vision.SecurityOperationsService.Application.Assets.Queries;

// Shared nested DTOs
public sealed record BuildingDto(Guid Id, string Name);

public sealed record LocationDto(Guid Id, string Name, string? Floor, string? Department);

// Asset list item — returned from GET /api/v1/assets
public sealed record AssetListItemDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string AssetType,
    string Status,
    BuildingDto Building,
    LocationDto Location,
    DateTimeOffset? LastServiceAt,
    DateTimeOffset? StatusChangedAt);

// Recent incident shown on asset detail
public sealed record AssetIncidentDto(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset CreatedAt,
    Guid? WorkOrderId);

// Full asset detail — returned from GET /api/v1/assets/{id}
public sealed record AssetDetailDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string AssetType,
    string Status,
    string? Manufacturer,
    string? Model,
    string? Description,
    BuildingDto Building,
    LocationDto Location,
    DateTimeOffset? LastServiceAt,
    DateTimeOffset? StatusChangedAt,
    IReadOnlyList<AssetIncidentDto> RecentIncidents);
