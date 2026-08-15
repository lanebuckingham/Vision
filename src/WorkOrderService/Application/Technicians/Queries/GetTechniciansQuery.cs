using MediatR;
using Vision.WorkOrderService.Application.Common;

namespace Vision.WorkOrderService.Application.Technicians.Queries;

public sealed record GetTechniciansQuery(
    bool ActiveOnly = true,
    string? Search = null,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<TechnicianListItemDto>>;
