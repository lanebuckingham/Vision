using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// P1 selective hardening: deterministic coverage for GetWorkOrderSummaryQueryHandler,
/// which drives the management UI's status counts. Uses its own isolated database with
/// known fixture data rather than the shared demo seed, so counts are exact.
/// </summary>
[Collection("PostgreSQL")]
public class WorkOrderSummaryTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_test_wo_summary;Username=vision;Password=vision_dev";

    private WorkOrderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkOrderDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new WorkOrderDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private static WorkOrder CreateWorkOrder(WorkOrderStatus status) => new()
    {
        Id = Guid.NewGuid(),
        SecurityAssetId = Guid.NewGuid(),
        Title = "Summary test work order",
        Description = "Fixture data for summary count testing.",
        Priority = WorkOrderPriority.Medium,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Summary_CountsEachStatusCorrectly()
    {
        await using var db = CreateContext();

        db.WorkOrders.AddRange(
            CreateWorkOrder(WorkOrderStatus.New),
            CreateWorkOrder(WorkOrderStatus.New),
            CreateWorkOrder(WorkOrderStatus.Assigned),
            CreateWorkOrder(WorkOrderStatus.InProgress),
            CreateWorkOrder(WorkOrderStatus.InProgress),
            CreateWorkOrder(WorkOrderStatus.InProgress),
            CreateWorkOrder(WorkOrderStatus.Completed));
        await db.SaveChangesAsync();

        var handler = new GetWorkOrderSummaryQueryHandler(db);
        var summary = await handler.Handle(new GetWorkOrderSummaryQuery(), CancellationToken.None);

        Assert.Equal(2, summary.ByStatus.New);
        Assert.Equal(1, summary.ByStatus.Assigned);
        Assert.Equal(3, summary.ByStatus.InProgress);
        Assert.Equal(1, summary.ByStatus.Completed);
    }

    [Fact]
    public async Task Summary_OpenCount_ExcludesCompleted()
    {
        await using var db = CreateContext();

        db.WorkOrders.AddRange(
            CreateWorkOrder(WorkOrderStatus.New),
            CreateWorkOrder(WorkOrderStatus.Assigned),
            CreateWorkOrder(WorkOrderStatus.InProgress),
            CreateWorkOrder(WorkOrderStatus.Completed),
            CreateWorkOrder(WorkOrderStatus.Completed));
        await db.SaveChangesAsync();

        var handler = new GetWorkOrderSummaryQueryHandler(db);
        var summary = await handler.Handle(new GetWorkOrderSummaryQuery(), CancellationToken.None);

        // Open = New + Assigned + InProgress, deliberately excluding Completed.
        Assert.Equal(3, summary.OpenCount);
    }

    [Fact]
    public async Task Summary_NoWorkOrders_ReturnsAllZeroes()
    {
        await using var db = CreateContext();

        var handler = new GetWorkOrderSummaryQueryHandler(db);
        var summary = await handler.Handle(new GetWorkOrderSummaryQuery(), CancellationToken.None);

        Assert.Equal(0, summary.OpenCount);
        Assert.Equal(0, summary.ByStatus.New);
        Assert.Equal(0, summary.ByStatus.Assigned);
        Assert.Equal(0, summary.ByStatus.InProgress);
        Assert.Equal(0, summary.ByStatus.Completed);
    }
}
