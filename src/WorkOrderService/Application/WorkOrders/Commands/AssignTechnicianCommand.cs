using MediatR;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed record AssignTechnicianCommand(
    Guid WorkOrderId,
    Guid TechnicianId) : IRequest<WorkOrderDetailDto>;
