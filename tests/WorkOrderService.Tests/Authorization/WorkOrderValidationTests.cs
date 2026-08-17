using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

namespace Vision.WorkOrderService.Tests.Authorization;

/// <summary>
/// P1 selective hardening: validation regression for manager/technician actions where
/// invalid input could otherwise surface as an unhandled 500, plus list-query validation
/// that was not previously exercised at the API level. Reuses WorkOrderAuthFactory since
/// it already provisions a real Postgres-backed WorkOrderDbContext with SecurityManager
/// access.
/// </summary>
[Collection("WorkOrderAuth")]
public class WorkOrderValidationTests : IAsyncLifetime
{
    private readonly WorkOrderAuthFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReady();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AssignTechnician_UnknownTechnicianId_Returns404()
    {
        var workOrderId = await SeedWorkOrderAsync(status: WorkOrderStatus.New);

        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/assignment",
            new { technicianId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignTechnician_InactiveTechnician_Returns409AndDoesNotAssign()
    {
        var technicianId = await SeedTechnicianAsync(isActive: false);
        var workOrderId = await SeedWorkOrderAsync(status: WorkOrderStatus.New);

        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/assignment",
            new { technicianId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await GetStoredWorkOrderAsync(workOrderId);
        Assert.Null(stored.AssignedTechnicianId);
        Assert.Equal(WorkOrderStatus.New, stored.Status);
    }

    [Fact]
    public async Task AddTechnicianNote_BlankContent_Returns400()
    {
        var subject = $"cognito-validation-{Guid.NewGuid():N}";
        var technicianId = await SeedTechnicianAsync(isActive: true, cognitoSubject: subject);
        var workOrderId = await SeedWorkOrderAsync(status: WorkOrderStatus.InProgress, assignedTechnicianId: technicianId);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/notes",
            new { content = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTechnicianNote_ContentExceedsMaxLength_Returns400()
    {
        var subject = $"cognito-validation-{Guid.NewGuid():N}";
        var technicianId = await SeedTechnicianAsync(isActive: true, cognitoSubject: subject);
        var workOrderId = await SeedWorkOrderAsync(status: WorkOrderStatus.InProgress, assignedTechnicianId: technicianId);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/notes",
            new { content = new string('a', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteWorkOrder_NoSummaryAndNoNotes_Returns400AndLeavesStatusUnchanged()
    {
        var subject = $"cognito-validation-{Guid.NewGuid():N}";
        var technicianId = await SeedTechnicianAsync(isActive: true, cognitoSubject: subject);
        var workOrderId = await SeedWorkOrderAsync(status: WorkOrderStatus.InProgress, assignedTechnicianId: technicianId);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/complete",
            new { completionSummary = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stored = await GetStoredWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
    }

    [Fact]
    public async Task CreateWorkOrder_InvalidPriority_Returns400()
    {
        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/work-orders", new
        {
            securityAssetId = Guid.NewGuid(),
            title = "Validation test",
            description = "Testing invalid priority.",
            priority = "Extreme"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrders_InvalidStatus_Returns400()
    {
        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync("/api/v1/work-orders?status=NotAStatus");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrders_InvalidPriority_Returns400()
    {
        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync("/api/v1/work-orders?priority=Extreme");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrders_PageLessThanOne_Returns400()
    {
        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync("/api/v1/work-orders?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkOrders_PageSizeExceedsMaximum_Returns400()
    {
        _factory.SetIdentity("sm-validation", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync("/api/v1/work-orders?pageSize=500");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // === Helpers ===

    private async Task<Guid> SeedTechnicianAsync(bool isActive, string? cognitoSubject = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        var technician = new Technician
        {
            Id = Guid.NewGuid(),
            DisplayName = $"Validation Test Technician {Guid.NewGuid():N}"[..40],
            Email = $"{Guid.NewGuid():N}@validationtest.vision.local",
            IsActive = isActive,
            Specialty = "Validation Test",
            CognitoSubject = cognitoSubject ?? $"cognito-validation-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.Technicians.Add(technician);
        await db.SaveChangesAsync();
        return technician.Id;
    }

    private async Task<Guid> SeedWorkOrderAsync(WorkOrderStatus status, Guid? assignedTechnicianId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        var now = DateTimeOffset.UtcNow;
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = SeedDataIds.PharmacyStorageCamera02,
            Title = "Validation test work order",
            Description = "Isolated work order used by WorkOrderValidationTests.",
            Priority = WorkOrderPriority.Medium,
            Status = status,
            AssignedTechnicianId = assignedTechnicianId,
            AssignedAt = assignedTechnicianId is null ? null : now.AddHours(-3),
            StartedAt = status is WorkOrderStatus.InProgress or WorkOrderStatus.Completed ? now.AddHours(-2) : null,
            AssetNameSnapshot = "VALIDTEST Camera 01",
            LocationNameSnapshot = "Validation Test Location",
            CreatedAt = now.AddHours(-4),
            UpdatedAt = now.AddHours(-4)
        };

        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();
        return workOrder.Id;
    }

    private async Task<WorkOrder> GetStoredWorkOrderAsync(Guid workOrderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        return await db.WorkOrders.AsNoTracking().FirstAsync(w => w.Id == workOrderId);
    }
}
