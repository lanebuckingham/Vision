using MediatR;
using Vision.WorkOrderService.Application.Common;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed record GetWorkOrdersQuery(
    string? Status,
    string? Priority,
    Guid? TechnicianId,
    Guid? AssetId,
    Guid? IncidentId,
    string? Search,
    int Page = 1,
    int PageSize = 25) : IRequest<PagedList<WorkOrderListItemDto>>;
