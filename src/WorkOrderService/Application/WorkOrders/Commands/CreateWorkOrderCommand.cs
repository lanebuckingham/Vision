using MediatR;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed record CreateWorkOrderCommand(
    Guid SecurityAssetId,
    Guid? SecurityIncidentId,
    string Title,
    string Description,
    string Priority,
    string? AssetName,
    string? LocationName) : IRequest<WorkOrderDetailDto>;
