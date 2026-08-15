using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Queries;

public sealed class GetWorkOrderSummaryQueryHandler(WorkOrderDbContext db)
    : IRequestHandler<GetWorkOrderSummaryQuery, WorkOrderSummaryDto>
{
    public async Task<WorkOrderSummaryDto> Handle(
        GetWorkOrderSummaryQuery request, CancellationToken cancellationToken)
    {
        var counts = await db.WorkOrders
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                New = g.Count(w => w.Status == WorkOrderStatus.New),
                Assigned = g.Count(w => w.Status == WorkOrderStatus.Assigned),
                InProgress = g.Count(w => w.Status == WorkOrderStatus.InProgress),
                Completed = g.Count(w => w.Status == WorkOrderStatus.Completed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var newCount = counts?.New ?? 0;
        var assignedCount = counts?.Assigned ?? 0;
        var inProgressCount = counts?.InProgress ?? 0;
        var completedCount = counts?.Completed ?? 0;

        return new WorkOrderSummaryDto(
            OpenCount: newCount + assignedCount + inProgressCount,
            ByStatus: new WorkOrderStatusCountsDto(
                newCount,
                assignedCount,
                inProgressCount,
                completedCount));
    }
}
