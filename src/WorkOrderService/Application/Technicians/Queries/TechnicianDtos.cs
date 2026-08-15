namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed record TechnicianListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? Specialty,
    bool IsActive);

public sealed record TechnicianDetailDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? Specialty,
    bool IsActive,
    DateTimeOffset CreatedAt);
