using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Tests.Domain;

public class TechnicianNoteTests
{
    private static WorkOrder CreateInProgressWorkOrder()
    {
        var wo = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = Guid.NewGuid(),
            Title = "Test",
            Description = "Desc",
            Priority = WorkOrderPriority.High,
            Status = WorkOrderStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var tech = new Technician
        {
            Id = Guid.NewGuid(),
            DisplayName = "Tech",
            Email = "t@t.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        wo.AssignTechnician(tech);
        wo.StartWork();
        return wo;
    }

    [Fact]
    public void AddNote_WhenInProgress_Succeeds()
    {
        var wo = CreateInProgressWorkOrder();
        var techId = wo.AssignedTechnicianId!.Value;

        wo.AddNote(techId, "Fixed the cable");

        Assert.Single(wo.Notes);
        Assert.Equal("Fixed the cable", wo.Notes.First().Content);
        Assert.Equal(techId, wo.Notes.First().TechnicianId);
        Assert.NotEqual(Guid.Empty, wo.Notes.First().Id);
    }

    [Fact]
    public void AddNote_WhenAssigned_Succeeds()
    {
        var wo = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = Guid.NewGuid(),
            Title = "Test",
            Description = "Desc",
            Priority = WorkOrderPriority.Low,
            Status = WorkOrderStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var tech = new Technician { Id = Guid.NewGuid(), DisplayName = "T", Email = "t@t.com", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        wo.AssignTechnician(tech);

        wo.AddNote(tech.Id, "Note while assigned");

        Assert.Single(wo.Notes);
    }

    [Fact]
    public void AddNote_WhenNew_Throws()
    {
        var wo = new WorkOrder
        {
            Id = Guid.NewGuid(),
            SecurityAssetId = Guid.NewGuid(),
            Title = "Test",
            Description = "Desc",
            Priority = WorkOrderPriority.Low,
            Status = WorkOrderStatus.New,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        Assert.Throws<InvalidOperationException>(() => wo.AddNote(Guid.NewGuid(), "Note"));
    }

    [Fact]
    public void AddNote_WhenCompleted_Throws()
    {
        var wo = CreateInProgressWorkOrder();
        wo.Complete("Done");

        Assert.Throws<InvalidOperationException>(() => wo.AddNote(Guid.NewGuid(), "Late note"));
    }

    [Fact]
    public void AddNote_WhenBlankContent_Throws()
    {
        var wo = CreateInProgressWorkOrder();

        Assert.Throws<ArgumentException>(() => wo.AddNote(wo.AssignedTechnicianId!.Value, "  "));
    }
}
