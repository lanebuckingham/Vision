using MediatR;
using Vision.SecurityOperationsService.Application.Common;

namespace Vision.SecurityOperationsService.Application.Incidents.Queries;

public sealed record GetIncidentsQuery(
    string? Status,
    string? Severity,
    Guid? AssetId,
    Guid? BuildingId,
    Guid? LocationId,
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<IncidentListItemDto>>;
