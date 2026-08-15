using MediatR;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed record GetWorkOrderSummaryQuery : IRequest<WorkOrderSummaryDto>;

public sealed record WorkOrderSummaryDto(
    int OpenCount,
    WorkOrderStatusCountsDto ByStatus);

public sealed record WorkOrderStatusCountsDto(
    int New,
    int Assigned,
    int InProgress,
    int Completed);
