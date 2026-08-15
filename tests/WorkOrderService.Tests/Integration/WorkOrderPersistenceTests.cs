using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;

namespace Vision.WorkOrderService.Tests.Integration;

[Collection("PostgreSQL")]
public class WorkOrderPersistenceTests(PostgresFixture fixture) : IAsyncLifetime
{
    private WorkOrderDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = fixture.CreateContext();
        // Clean work orders between tests
        _db.WorkOrders.RemoveRange(await _db.WorkOrders.ToListAsync());
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private static WorkOrder CreateWorkOrder(
        Guid? incidentId = null,
        Guid? sourceEventId = null) => new()
    {
        Id = Guid.NewGuid(),
        SecurityAssetId = Guid.NewGuid(),
        SecurityIncidentId = incidentId,
        SourceEventId = sourceEventId,
        Title = "Test WO",
        Description = "Test description",
        Priority = WorkOrderPriority.Critical,
        Status = WorkOrderStatus.New,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task UniqueSecurityIncidentId_RejectsDuplicate()
    {
        var incidentId = Guid.NewGuid();

        _db.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));
        await _db.SaveChangesAsync();

        // Second insert with same incident ID must fail
        await using var db2 = fixture.CreateContext();
        db2.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => db2.SaveChangesAsync());

        Assert.Contains("23505", ex.InnerException?.Message ?? "");
    }

    [Fact]
    public async Task UniqueSecurityIncidentId_AllowsMultipleNulls()
    {
        _db.WorkOrders.Add(CreateWorkOrder(incidentId: null));
        _db.WorkOrders.Add(CreateWorkOrder(incidentId: null));

        await _db.SaveChangesAsync(); // Should not throw

        Assert.Equal(2, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task UniqueSourceEventId_RejectsDuplicate()
    {
        var eventId = Guid.NewGuid();

        _db.WorkOrders.Add(CreateWorkOrder(sourceEventId: eventId));
        await _db.SaveChangesAsync();

        await using var db2 = fixture.CreateContext();
        db2.WorkOrders.Add(CreateWorkOrder(sourceEventId: eventId));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => db2.SaveChangesAsync());

        Assert.Contains("23505", ex.InnerException?.Message ?? "");
    }

    [Fact]
    public async Task UniqueSourceEventId_AllowsMultipleNulls()
    {
        _db.WorkOrders.Add(CreateWorkOrder(sourceEventId: null));
        _db.WorkOrders.Add(CreateWorkOrder(sourceEventId: null));

        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task ConcurrentDuplicateInsert_OneSucceeds()
    {
        var incidentId = Guid.NewGuid();

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        db1.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));
        db2.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));

        // One must succeed, the other must throw unique violation
        var task1 = db1.SaveChangesAsync();
        var task2 = db2.SaveChangesAsync();

        var results = await Task.WhenAll(
            Task.Run(async () => { try { await task1; return true; } catch (DbUpdateException) { return false; } }),
            Task.Run(async () => { try { await task2; return true; } catch (DbUpdateException) { return false; } }));

        // Exactly one succeeded
        Assert.Equal(1, results.Count(r => r));
    }

    [Fact]
    public async Task ManualDuplicateIncidentConflict_ReturnsConflict()
    {
        var incidentId = Guid.NewGuid();

        _db.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));
        await _db.SaveChangesAsync();

        // Simulate second insert that passes pre-check but hits constraint
        await using var db2 = fixture.CreateContext();
        db2.WorkOrders.Add(CreateWorkOrder(incidentId: incidentId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task OwnedTechnicianNotes_PersistCorrectly()
    {
        var techId = Guid.NewGuid();
        var wo = CreateWorkOrder();
        var tech = new Technician
        {
            Id = techId,
            DisplayName = "Test Tech",
            Email = "test@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Technicians.Add(tech);
        wo.AssignTechnician(tech);
        wo.StartWork();
        wo.AddNote(techId, "First note");
        wo.AddNote(techId, "Second note");

        _db.WorkOrders.Add(wo);
        await _db.SaveChangesAsync();

        // Reload from fresh context
        await using var db2 = fixture.CreateContext();
        var loaded = await db2.WorkOrders
            .Include(w => w.Notes)
            .FirstAsync(w => w.Id == wo.Id);

        Assert.Equal(2, loaded.Notes.Count);
        Assert.All(loaded.Notes, n => Assert.Equal(techId, n.TechnicianId));
    }

    [Fact]
    public async Task WorkOrdersSchema_IsCorrect()
    {
        var count = await _db.Database
            .SqlQueryRaw<int>(
                "SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'work_orders' AND table_name = 'work_orders'")
            .FirstAsync();

        Assert.Equal(1, count);
    }
}

[CollectionDefinition("PostgreSQL")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
