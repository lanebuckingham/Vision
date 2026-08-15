using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Tests.Domain;

public class WorkOrderLifecycleTests
{
    private static WorkOrder CreateNewWorkOrder() => new()
    {
        Id = Guid.NewGuid(),
        SecurityAssetId = Guid.NewGuid(),
        Title = "Test work order",
        Description = "Test description",
        Priority = WorkOrderPriority.Critical,
        Status = WorkOrderStatus.New,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static Technician CreateActiveTechnician() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Test Tech",
        Email = "tech@test.com",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Technician CreateInactiveTechnician() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Inactive Tech",
        Email = "inactive@test.com",
        IsActive = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    // --- Assignment Tests ---

    [Fact]
    public void AssignTechnician_WhenNewAndActive_TransitionsToAssigned()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateActiveTechnician();

        wo.AssignTechnician(tech);

        Assert.Equal(WorkOrderStatus.Assigned, wo.Status);
        Assert.Equal(tech.Id, wo.AssignedTechnicianId);
        Assert.NotNull(wo.AssignedAt);
    }

    [Fact]
    public void AssignTechnician_WhenInactive_Throws()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateInactiveTechnician();

        var ex = Assert.Throws<InvalidOperationException>(() => wo.AssignTechnician(tech));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssignTechnician_WhenAlreadyAssigned_Throws()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateActiveTechnician();
        wo.AssignTechnician(tech);

        var ex = Assert.Throws<InvalidOperationException>(() => wo.AssignTechnician(tech));
        Assert.Contains("New", ex.Message);
    }

    [Fact]
    public void AssignTechnician_WhenInProgress_Throws()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateActiveTechnician();
        wo.AssignTechnician(tech);
        wo.StartWork();

        Assert.Throws<InvalidOperationException>(() => wo.AssignTechnician(tech));
    }

    [Fact]
    public void AssignTechnician_WhenCompleted_Throws()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateActiveTechnician();
        wo.AssignTechnician(tech);
        wo.StartWork();
        wo.Complete("Done");

        Assert.Throws<InvalidOperationException>(() => wo.AssignTechnician(tech));
    }

    // --- StartWork Tests ---

    [Fact]
    public void StartWork_WhenAssigned_TransitionsToInProgress()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());

        wo.StartWork();

        Assert.Equal(WorkOrderStatus.InProgress, wo.Status);
        Assert.NotNull(wo.StartedAt);
    }

    [Fact]
    public void StartWork_WhenNew_Throws()
    {
        var wo = CreateNewWorkOrder();
        Assert.Throws<InvalidOperationException>(() => wo.StartWork());
    }

    [Fact]
    public void StartWork_WhenInProgress_Throws()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();

        Assert.Throws<InvalidOperationException>(() => wo.StartWork());
    }

    [Fact]
    public void StartWork_WhenCompleted_Throws()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();
        wo.Complete("Done");

        Assert.Throws<InvalidOperationException>(() => wo.StartWork());
    }

    // --- Complete Tests ---

    [Fact]
    public void Complete_WhenInProgressWithSummary_TransitionsToCompleted()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();

        wo.Complete("Repair completed.");

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.Equal("Repair completed.", wo.CompletionSummary);
        Assert.NotNull(wo.CompletedAt);
    }

    [Fact]
    public void Complete_WhenInProgressWithNoteAndBlankSummary_Succeeds()
    {
        var wo = CreateNewWorkOrder();
        var tech = CreateActiveTechnician();
        wo.AssignTechnician(tech);
        wo.StartWork();
        wo.AddNote(tech.Id, "Fixed it");

        wo.Complete(null);

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.NotNull(wo.CompletedAt);
    }

    [Fact]
    public void Complete_WhenNoSummaryAndNoNotes_Throws()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();

        var ex = Assert.Throws<InvalidOperationException>(() => wo.Complete(null));
        Assert.Contains("summary", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Complete_WhenNew_Throws()
    {
        var wo = CreateNewWorkOrder();
        Assert.Throws<InvalidOperationException>(() => wo.Complete("Summary"));
    }

    [Fact]
    public void Complete_WhenAssigned_Throws()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());

        Assert.Throws<InvalidOperationException>(() => wo.Complete("Summary"));
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_Throws()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();
        wo.Complete("Done");

        Assert.Throws<InvalidOperationException>(() => wo.Complete("Again"));
    }

    [Fact]
    public void Complete_DoesNotOverwriteOriginalTimestamp()
    {
        var wo = CreateNewWorkOrder();
        wo.AssignTechnician(CreateActiveTechnician());
        wo.StartWork();
        wo.Complete("First");

        var originalCompletedAt = wo.CompletedAt;

        Assert.Throws<InvalidOperationException>(() => wo.Complete("Second"));
        Assert.Equal(originalCompletedAt, wo.CompletedAt);
        Assert.Equal("First", wo.CompletionSummary);
    }
}
