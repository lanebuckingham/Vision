using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Application.WorkOrders.Queries;
using Vision.WorkOrderService.Domain;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

namespace Vision.WorkOrderService.Tests.Authorization;

/// <summary>
/// Authorization integration tests for WorkOrderService.
///
/// Approved matrix:
///   - Supervisory endpoints (summary, create, assignment, technician directory) require
///     WorkOrderManager (SecurityManager group only).
///   - Repair endpoints (start, notes, complete) require TechnicianWork (Technician group only)
///     AND the work order must be assigned to the authenticated technician.
///   - List/detail are dual-access: SecurityManager sees everything, Technician sees only
///     work orders assigned to them.
///
/// Ownership tests seed isolated Technicians and WorkOrders so the assertions never depend
/// on shared seed rows mutated by other tests.
/// </summary>
public class WorkOrderAuthorizationTests : IAsyncLifetime
{
    private readonly WorkOrderAuthFactory _factory = new();

    public async Task InitializeAsync() => await _factory.EnsureDatabaseReady();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // === 401 — Unauthenticated ===

    [Theory]
    [InlineData("/api/v1/work-orders")]
    [InlineData("/api/v1/work-orders/summary")]
    [InlineData("/api/v1/technicians")]
    public async Task Unauthenticated_Returns401(string path)
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_StartWork_Returns401()
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        var response = await client.PostAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderAssigned}/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_Unauthenticated_Returns200()
    {
        _factory.ClearIdentity();
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    // === SecurityManager allowed ===

    [Theory]
    [InlineData("/api/v1/work-orders")]
    [InlineData("/api/v1/work-orders/summary")]
    [InlineData("/api/v1/technicians")]
    public async Task SecurityManager_SupervisoryReads_Returns200(string path)
    {
        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SecurityManager_ListWorkOrders_SeesAllTechniciansWork()
    {
        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        // Queried per technician so the assertion does not depend on total row count.
        var marcus = await GetWorkOrderPageAsync(
            client, $"/api/v1/work-orders?pageSize=100&technicianId={SeedDataIds.TechMarcusJohnson}");
        var david = await GetWorkOrderPageAsync(
            client, $"/api/v1/work-orders?pageSize=100&technicianId={SeedDataIds.TechDavidPark}");
        var lisa = await GetWorkOrderPageAsync(
            client, $"/api/v1/work-orders?pageSize=100&technicianId={SeedDataIds.TechLisaReeves}");

        Assert.Contains(marcus.Items, w => w.Id == SeedDataIds.WorkOrderInProgress);
        Assert.Contains(david.Items, w => w.Id == SeedDataIds.WorkOrderAssigned);
        Assert.Contains(lisa.Items, w => w.Id == SeedDataIds.WorkOrderCompleted);
    }

    [Fact]
    public async Task SecurityManager_AnyTechniciansWorkOrderDetail_Returns200()
    {
        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderInProgress}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === SecurityManager denied from Technician-only repair actions ===

    [Fact]
    public async Task SecurityManager_StartWork_Returns403AndStateUnchanged()
    {
        var technicianId = await SeedTechnicianAsync(NewSubject("start-owner"));
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.Assigned);

        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsync($"/api/v1/work-orders/{workOrderId}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.Assigned, expectedNoteCount: 0, expectStarted: false);
    }

    [Fact]
    public async Task SecurityManager_AddNote_Returns403AndNotesUnchanged()
    {
        var technicianId = await SeedTechnicianAsync(NewSubject("note-owner"));
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.InProgress);

        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/notes",
            new { content = "Manager should not be able to add a technician note." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.InProgress, expectedNoteCount: 0, expectStarted: true);
    }

    [Fact]
    public async Task SecurityManager_CompleteWork_Returns403AndStateUnchanged()
    {
        var technicianId = await SeedTechnicianAsync(NewSubject("complete-owner"));
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.InProgress);

        _factory.SetIdentity("sm-user", "SecurityManager");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/complete",
            new { completionSummary = "Manager should not be able to complete repair work." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.InProgress, expectedNoteCount: 0, expectStarted: true);
    }

    // === Technician list scoping ===

    [Fact]
    public async Task Technician_ListWorkOrders_ReturnsOnlyOwnWorkOrders()
    {
        // Marcus owns WorkOrderInProgress. David owns WorkOrderAssigned, Lisa owns WorkOrderCompleted.
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        var page = await GetWorkOrderPageAsync(client, "/api/v1/work-orders?pageSize=100");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item =>
        {
            Assert.NotNull(item.AssignedTechnician);
            Assert.Equal(SeedDataIds.TechMarcusJohnson, item.AssignedTechnician!.Id);
        });

        Assert.Contains(page.Items, w => w.Id == SeedDataIds.WorkOrderInProgress);
        Assert.DoesNotContain(page.Items, w => w.Id == SeedDataIds.WorkOrderAssigned);
        Assert.DoesNotContain(page.Items, w => w.Id == SeedDataIds.WorkOrderCompleted);
        Assert.DoesNotContain(page.Items, w => w.Id == SeedDataIds.WorkOrderNew);
    }

    [Fact]
    public async Task Technician_ListWithOtherTechnicianIdFilter_CannotWidenResults()
    {
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        // Client-supplied technicianId must be ignored in favour of the authenticated identity.
        var page = await GetWorkOrderPageAsync(
            client,
            $"/api/v1/work-orders?pageSize=100&technicianId={SeedDataIds.TechDavidPark}");

        Assert.All(page.Items, item =>
        {
            Assert.NotNull(item.AssignedTechnician);
            Assert.Equal(SeedDataIds.TechMarcusJohnson, item.AssignedTechnician!.Id);
        });

        Assert.DoesNotContain(page.Items, w => w.Id == SeedDataIds.WorkOrderAssigned);
        Assert.Contains(page.Items, w => w.Id == SeedDataIds.WorkOrderInProgress);
    }

    // === Technician detail ownership ===

    [Fact]
    public async Task Technician_OwnWorkOrderDetail_Returns200()
    {
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderInProgress}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<WorkOrderDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(SeedDataIds.TechMarcusJohnson, detail!.AssignedTechnician?.Id);
    }

    [Fact]
    public async Task Technician_OtherTechnicianWorkOrderDetail_Returns403()
    {
        // WorkOrderAssigned belongs to David Park, not Marcus.
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderAssigned}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_UnassignedWorkOrderDetail_Returns403()
    {
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderNew}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === Technician start ownership ===

    [Fact]
    public async Task Technician_StartOwnWorkOrder_Returns200AndTransitions()
    {
        var subject = NewSubject("start-own");
        var technicianId = await SeedTechnicianAsync(subject);
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.Assigned);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsync($"/api/v1/work-orders/{workOrderId}/start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await GetStoredWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.InProgress, stored.Status);
        Assert.NotNull(stored.StartedAt);
    }

    [Fact]
    public async Task Technician_StartOtherTechniciansWorkOrder_Returns403AndStateUnchanged()
    {
        var callerSubject = NewSubject("start-caller");
        await SeedTechnicianAsync(callerSubject);

        var ownerId = await SeedTechnicianAsync(NewSubject("start-other-owner"));
        var workOrderId = await SeedWorkOrderAsync(ownerId, WorkOrderStatus.Assigned);

        _factory.SetIdentity(callerSubject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsync($"/api/v1/work-orders/{workOrderId}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.Assigned, expectedNoteCount: 0, expectStarted: false);
    }

    // === Technician note ownership ===

    [Fact]
    public async Task Technician_AddNoteToOwnWorkOrder_Returns201AndPersistsNote()
    {
        var subject = NewSubject("note-own");
        var technicianId = await SeedTechnicianAsync(subject);
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.InProgress);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/notes",
            new { content = "Replaced the PoE injector and verified the feed." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await CountNotesAsync(workOrderId));
    }

    [Fact]
    public async Task Technician_AddNoteToOtherTechniciansWorkOrder_Returns403AndNotesUnchanged()
    {
        var callerSubject = NewSubject("note-caller");
        await SeedTechnicianAsync(callerSubject);

        var ownerId = await SeedTechnicianAsync(NewSubject("note-other-owner"));
        var workOrderId = await SeedWorkOrderAsync(ownerId, WorkOrderStatus.InProgress);

        _factory.SetIdentity(callerSubject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/notes",
            new { content = "This note must never be recorded." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.InProgress, expectedNoteCount: 0, expectStarted: true);
    }

    // === Technician complete ownership ===

    [Fact]
    public async Task Technician_CompleteOwnWorkOrder_Returns200AndTransitions()
    {
        var subject = NewSubject("complete-own");
        var technicianId = await SeedTechnicianAsync(subject);
        var workOrderId = await SeedWorkOrderAsync(technicianId, WorkOrderStatus.InProgress);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/complete",
            new { completionSummary = "Camera feed restored and verified over a full recording cycle." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = await GetStoredWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
    }

    [Fact]
    public async Task Technician_CompleteOtherTechniciansWorkOrder_Returns403AndStateUnchanged()
    {
        var callerSubject = NewSubject("complete-caller");
        await SeedTechnicianAsync(callerSubject);

        var ownerId = await SeedTechnicianAsync(NewSubject("complete-other-owner"));
        var workOrderId = await SeedWorkOrderAsync(ownerId, WorkOrderStatus.InProgress);

        _factory.SetIdentity(callerSubject, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/complete",
            new { completionSummary = "Not my work order." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.InProgress, expectedNoteCount: 0, expectStarted: true);
    }

    // === Technician denied from supervisory actions ===

    [Fact]
    public async Task Technician_Summary_Returns403()
    {
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/work-orders/summary")).StatusCode);
    }

    [Fact]
    public async Task Technician_TechnicianDirectory_Returns403()
    {
        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/technicians")).StatusCode);
    }

    [Fact]
    public async Task Technician_CreateWorkOrder_Returns403AndDoesNotCreate()
    {
        var countBefore = await CountWorkOrdersAsync();

        _factory.SetIdentity(SeedDataIds.CognitoSubTechMarcus, "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsJsonAsync("/api/v1/work-orders", new
        {
            securityAssetId = SeedDataIds.PharmacyStorageCamera02,
            title = "Technician should not create this",
            description = "Denied work order creation attempt.",
            priority = "Low"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(countBefore, await CountWorkOrdersAsync());
    }

    [Fact]
    public async Task Technician_AssignTechnician_Returns403AndAssignmentUnchanged()
    {
        var subject = NewSubject("assign-caller");
        var callerTechnicianId = await SeedTechnicianAsync(subject);
        var workOrderId = await SeedWorkOrderAsync(assignedTechnicianId: null, WorkOrderStatus.New);

        _factory.SetIdentity(subject, "Technician");
        using var client = _factory.CreateDefaultClient();

        // Assignment is a supervisory action: a Technician may not assign work, not even to themselves.
        var response = await client.PostAsJsonAsync(
            $"/api/v1/work-orders/{workOrderId}/assignment",
            new { technicianId = callerTechnicianId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stored = await GetStoredWorkOrderAsync(workOrderId);
        Assert.Equal(WorkOrderStatus.New, stored.Status);
        Assert.Null(stored.AssignedTechnicianId);
        Assert.Null(stored.AssignedAt);
    }

    // === Technician with no CognitoSubject mapping ===

    [Fact]
    public async Task Technician_NoMapping_ListWorkOrders_Returns403()
    {
        _factory.SetIdentity("unmapped-technician-sub", "Technician");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/work-orders")).StatusCode);
    }

    [Fact]
    public async Task Technician_NoMapping_WorkOrderDetail_Returns403()
    {
        _factory.SetIdentity("unmapped-technician-sub", "Technician");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderAssigned}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Technician_NoMapping_StartWork_Returns403AndStateUnchanged()
    {
        var ownerId = await SeedTechnicianAsync(NewSubject("unmapped-owner"));
        var workOrderId = await SeedWorkOrderAsync(ownerId, WorkOrderStatus.Assigned);

        _factory.SetIdentity("unmapped-technician-sub", "Technician");
        using var client = _factory.CreateDefaultClient();

        var response = await client.PostAsync($"/api/v1/work-orders/{workOrderId}/start", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertWorkOrderUnchangedAsync(workOrderId, WorkOrderStatus.Assigned, expectedNoteCount: 0, expectStarted: false);
    }

    // === CredentialAdministrator denied from WorkOrderService ===

    [Theory]
    [InlineData("/api/v1/work-orders")]
    [InlineData("/api/v1/work-orders/summary")]
    [InlineData("/api/v1/technicians")]
    public async Task CredentialAdministrator_Returns403(string path)
    {
        _factory.SetIdentity("ca-user", "CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task CredentialAdministrator_WorkOrderDetail_Returns403()
    {
        _factory.SetIdentity("ca-user", "CredentialAdministrator");
        using var client = _factory.CreateDefaultClient();
        var response = await client.GetAsync($"/api/v1/work-orders/{SeedDataIds.WorkOrderInProgress}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === No approved Vision role ===

    [Fact]
    public async Task AuthenticatedWithNoApprovedRole_ListWorkOrders_Returns403()
    {
        _factory.SetIdentity("stranger", "SomeUnrelatedGroup");
        using var client = _factory.CreateDefaultClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/work-orders")).StatusCode);
    }

    // === Helpers ===

    private static string NewSubject(string label) => $"cognito-authtest-{label}-{Guid.NewGuid():N}";

    private static async Task<PagedList<WorkOrderListItemDto>> GetWorkOrderPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedList<WorkOrderListItemDto>>();
        Assert.NotNull(page);
        return page!;
    }

    private async Task<Guid> SeedTechnicianAsync(string cognitoSubject)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        var technician = new Technician
        {
            Id = Guid.NewGuid(),
            DisplayName = $"Auth Test Technician {cognitoSubject[^8..]}",
            Email = $"{Guid.NewGuid():N}@authtest.vision.local",
            IsActive = true,
            Specialty = "Authorization Test",
            CognitoSubject = cognitoSubject,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        db.Technicians.Add(technician);
        await db.SaveChangesAsync();
        return technician.Id;
    }

    private async Task<Guid> SeedWorkOrderAsync(Guid? assignedTechnicianId, WorkOrderStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();

        var now = DateTimeOffset.UtcNow;
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = SeedDataIds.PharmacyStorageCamera02,
            Title = "Authorization test work order",
            Description = "Isolated work order used by WorkOrderService authorization tests.",
            Priority = WorkOrderPriority.Medium,
            Status = status,
            AssignedTechnicianId = assignedTechnicianId,
            AssignedAt = assignedTechnicianId is null ? null : now.AddHours(-3),
            StartedAt = status is WorkOrderStatus.InProgress or WorkOrderStatus.Completed ? now.AddHours(-2) : null,
            CompletedAt = status == WorkOrderStatus.Completed ? now.AddHours(-1) : null,
            AssetNameSnapshot = "AUTHTEST Camera 01",
            LocationNameSnapshot = "Authorization Test Location",
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

    /// <summary>
    /// TechnicianNote is an owned collection of WorkOrder, so it is counted through the owner.
    /// </summary>
    private async Task<int> CountNotesAsync(Guid workOrderId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        return await db.WorkOrders
            .AsNoTracking()
            .Where(w => w.Id == workOrderId)
            .Select(w => w.Notes.Count)
            .FirstAsync();
    }

    private async Task<int> CountWorkOrdersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        return await db.WorkOrders.AsNoTracking().CountAsync();
    }

    /// <summary>
    /// Asserts a denied mutation left the work order untouched: same status, same note
    /// collection, and no new lifecycle timestamps.
    /// </summary>
    private async Task AssertWorkOrderUnchangedAsync(
        Guid workOrderId,
        WorkOrderStatus expectedStatus,
        int expectedNoteCount,
        bool expectStarted)
    {
        var stored = await GetStoredWorkOrderAsync(workOrderId);

        Assert.Equal(expectedStatus, stored.Status);
        Assert.Equal(expectedNoteCount, await CountNotesAsync(workOrderId));
        Assert.Null(stored.CompletedAt);
        Assert.Null(stored.CompletionSummary);

        if (expectStarted)
            Assert.NotNull(stored.StartedAt);
        else
            Assert.Null(stored.StartedAt);
    }
}
