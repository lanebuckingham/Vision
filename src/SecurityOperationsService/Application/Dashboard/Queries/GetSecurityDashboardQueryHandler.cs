using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Domain;
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Application.Dashboard.Queries;

public sealed class GetSecurityDashboardQueryHandler(SecurityOperationsDbContext db)
    : IRequestHandler<GetSecurityDashboardQuery, SecurityDashboardDto>
{
    private const int CriticalAlertLimit = 5;
    private const int RecentActivityLimit = 10;

    public async Task<SecurityDashboardDto> Handle(
        GetSecurityDashboardQuery request, CancellationToken cancellationToken)
    {
        // Hospital
        var hospital = await db.Hospitals
            .AsNoTracking()
            .Select(h => new DashboardHospitalDto(h.Id, h.Name))
            .FirstAsync(cancellationToken);

        // Security health — aggregate asset counts
        var assetCounts = await db.SecurityAssets
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Operational = g.Count(a => a.Status == SecurityAssetStatus.Operational),
                Degraded = g.Count(a => a.Status == SecurityAssetStatus.Degraded),
                Offline = g.Count(a => a.Status == SecurityAssetStatus.Offline)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalAssets = assetCounts?.Total ?? 0;
        var operationalAssets = assetCounts?.Operational ?? 0;
        var operationalPercentage = totalAssets > 0
            ? (int)Math.Round(100.0 * operationalAssets / totalAssets)
            : 0;

        var securityHealth = new SecurityHealthDto(
            operationalPercentage,
            operationalAssets,
            totalAssets,
            assetCounts?.Degraded ?? 0,
            assetCounts?.Offline ?? 0);

        // Active incidents
        var activeCritical = await db.SecurityIncidents
            .AsNoTracking()
            .CountAsync(i => i.Severity == IncidentSeverity.Critical && i.Status != IncidentStatus.Resolved, cancellationToken);

        var activeTotal = await db.SecurityIncidents
            .AsNoTracking()
            .CountAsync(i => i.Status != IncidentStatus.Resolved, cancellationToken);

        var incidents = new DashboardIncidentsDto(activeCritical, activeTotal);

        // Critical alerts — active Critical incidents, newest first
        var criticalAlerts = await db.SecurityIncidents
            .AsNoTracking()
            .Where(i => i.Severity == IncidentSeverity.Critical && i.Status != IncidentStatus.Resolved)
            .OrderByDescending(i => i.CreatedAt)
            .Take(CriticalAlertLimit)
            .Select(i => new CriticalAlertDto(
                i.Id,
                i.Title,
                i.Severity.ToString(),
                i.Status.ToString(),
                i.SecurityAssetId,
                i.SecurityAsset != null ? i.SecurityAsset.Name : null,
                i.SecurityAsset != null ? i.SecurityAsset.AssetType.ToString() : null,
                i.Location.Name,
                i.CreatedAt))
            .ToListAsync(cancellationToken);

        // Recent activity — most recent incidents (created or resolved)
        var recentActivity = await db.SecurityIncidents
            .AsNoTracking()
            .OrderByDescending(i => i.UpdatedAt)
            .Take(RecentActivityLimit)
            .Select(i => new RecentActivityDto(
                i.Status == IncidentStatus.Resolved ? "IncidentResolved" : "IncidentCreated",
                i.Title,
                i.Status == IncidentStatus.Resolved ? i.ResolvedAt!.Value : i.CreatedAt,
                i.Id,
                i.SecurityAssetId))
            .ToListAsync(cancellationToken);

        return new SecurityDashboardDto(hospital, securityHealth, incidents, criticalAlerts, recentActivity);
    }
}
