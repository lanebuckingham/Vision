namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

// Shared nested DTOs
public sealed record AssignedTechnicianSummaryDto(
    Guid Id,
    string DisplayName,
    string? Specialty);

public sealed record AssignedTechnicianDetailDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? Specialty,
    bool IsActive);

public sealed record TechnicianNoteDto(
    Guid Id,
    Guid TechnicianId,
    string TechnicianDisplayName,
    string Content,
    DateTimeOffset CreatedAt);

// List item — no notes, no long description
public sealed record WorkOrderListItemDto(
    Guid Id,
    string Title,
    string Priority,
    string Status,
    Guid SecurityAssetId,
    Guid? SecurityIncidentId,
    string? AssetName,
    string? LocationName,
    AssignedTechnicianSummaryDto? AssignedTechnician,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Full detail — includes notes and description
public sealed record WorkOrderDetailDto(
    Guid Id,
    Guid SecurityAssetId,
    Guid? SecurityIncidentId,
    string Title,
    string Description,
    string Priority,
    string Status,
    string? AssetName,
    string? LocationName,
    AssignedTechnicianDetailDto? AssignedTechnician,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CompletionSummary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TechnicianNoteDto> Notes);
