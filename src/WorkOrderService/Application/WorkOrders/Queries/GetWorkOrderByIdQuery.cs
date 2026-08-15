using MediatR;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed record GetWorkOrderByIdQuery(Guid Id) : IRequest<WorkOrderDetailDto?>;
