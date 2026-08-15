using MediatR;
using Vision.WorkOrderService.Application.WorkOrders.Queries;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed record CompleteWorkOrderCommand(
    Guid WorkOrderId,
    string? CompletionSummary) : IRequest<WorkOrderDetailDto>;
