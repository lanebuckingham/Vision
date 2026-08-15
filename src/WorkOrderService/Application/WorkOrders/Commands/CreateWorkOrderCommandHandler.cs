using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Application.WorkOrders.Commands;

public sealed class CreateWorkOrderCommandHandler(
    WorkOrderDbContext db,
    ILogger<CreateWorkOrderCommandHandler> logger)
    : IRequestHandler<CreateWorkOrderCommand, WorkOrderDetailDto>
{
    public async Task<WorkOrderDetailDto> Handle(
        CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        // Check duplicate SecurityIncidentId
        if (request.SecurityIncidentId.HasValue)
        {
            var existingForIncident = await db.WorkOrders
                .AsNoTracking()
                .AnyAsync(w => w.SecurityIncidentId == request.SecurityIncidentId.Value, cancellationToken);

            if (existingForIncident)
                throw new InvalidOperationException(
                    $"A work order already exists for incident '{request.SecurityIncidentId.Value}'.");
        }

        var priority = Enum.Parse<WorkOrderPriority>(request.Priority, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = request.SecurityAssetId,
            SecurityIncidentId = request.SecurityIncidentId,
            Title = request.Title,
            Description = request.Description,
            Priority = priority,
            Status = WorkOrderStatus.New,
            AssetNameSnapshot = request.AssetName,
            LocationNameSnapshot = request.LocationName,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.WorkOrders.Add(workOrder);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new InvalidOperationException(
                $"A work order already exists for incident '{request.SecurityIncidentId}'.", ex);
        }

        logger.LogInformation(
            "Created manual work order {WorkOrderId} for asset {AssetId}",
            workOrder.Id, workOrder.SecurityAssetId);

        return new WorkOrderDetailDto(
            workOrder.Id,
            workOrder.SecurityAssetId,
            workOrder.SecurityIncidentId,
            workOrder.Title,
            workOrder.Description,
            workOrder.Priority.ToString(),
            workOrder.Status.ToString(),
            workOrder.AssetNameSnapshot,
            workOrder.LocationNameSnapshot,
            null,
            workOrder.AssignedAt,
            workOrder.StartedAt,
            workOrder.CompletedAt,
            workOrder.CompletionSummary,
            workOrder.CreatedAt,
            workOrder.UpdatedAt,
            []);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
