namespace Vision.SecurityOperationsService.Application.Dashboard.Queries;

public sealed record DashboardHospitalDto(Guid Id, string Name);

public sealed record SecurityHealthDto(
    int OperationalPercentage,
    int OperationalAssets,
    int TotalAssets,
    int DegradedAssets,
    int OfflineAssets);

public sealed record DashboardIncidentsDto(
    int ActiveCritical,
    int ActiveTotal);

public sealed record CriticalAlertDto(
    Guid IncidentId,
    string Title,
    string Severity,
    string Status,
    Guid? AssetId,
    string? AssetName,
    string? AssetType,
    string LocationName,
    DateTimeOffset CreatedAt);

public sealed record RecentActivityDto(
    string Type,
    string Title,
    DateTimeOffset OccurredAt,
    Guid? IncidentId,
    Guid? AssetId);

public sealed record SecurityDashboardDto(
    DashboardHospitalDto Hospital,
    SecurityHealthDto SecurityHealth,
    DashboardIncidentsDto Incidents,
    IReadOnlyList<CriticalAlertDto> CriticalAlerts,
    IReadOnlyList<RecentActivityDto> RecentActivity);
