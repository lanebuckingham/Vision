using MediatR;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed record StartWorkCommand(Guid WorkOrderId) : IRequest<WorkOrderDetailDto>;
