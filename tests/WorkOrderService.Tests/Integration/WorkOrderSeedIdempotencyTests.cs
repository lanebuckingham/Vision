using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

namespace Vision.WorkOrderService.Tests.Integration;

/// <summary>
/// P1 selective hardening: verifies repeated WorkOrderSeeder execution does not add
/// duplicates, mirroring the equivalent CredentialService seed-idempotency coverage.
/// Uses its own isolated database.
/// </summary>
[Collection("PostgreSQL")]
public class WorkOrderSeedIdempotencyTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Database=vision_wo_seed_test;Username=vision;Password=vision_dev";

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

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateData()
    {
        await using var db1 = CreateContext();
        await WorkOrderSeeder.SeedAsync(db1);

        var technicianCountAfterFirst = await db1.Technicians.CountAsync();
        var workOrderCountAfterFirst = await db1.WorkOrders.CountAsync();

        Assert.True(technicianCountAfterFirst > 0, "Seeder should create technicians");
        Assert.True(workOrderCountAfterFirst > 0, "Seeder should create work orders");

        await using var db2 = CreateContext();
        await WorkOrderSeeder.SeedAsync(db2);

        Assert.Equal(technicianCountAfterFirst, await db2.Technicians.CountAsync());
        Assert.Equal(workOrderCountAfterFirst, await db2.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_RunTwice_KnownSeedRowsRemainSingular()
    {
        await using var db1 = CreateContext();
        await WorkOrderSeeder.SeedAsync(db1);

        await using var db2 = CreateContext();
        await WorkOrderSeeder.SeedAsync(db2);

        var marcusCount = await db2.Technicians.CountAsync(t => t.Id == SeedDataIds.TechMarcusJohnson);
        Assert.Equal(1, marcusCount);

        var completedWorkOrderCount = await db2.WorkOrders.CountAsync(w => w.Id == SeedDataIds.WorkOrderCompleted);
        Assert.Equal(1, completedWorkOrderCount);
    }
}
