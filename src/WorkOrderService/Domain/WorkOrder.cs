namespace Vision.WorkOrderService.Domain;

public class WorkOrder
{
    public Guid Id { get; set; }
    public Guid SecurityAssetId { get; set; }
    public Guid? SecurityIncidentId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public WorkOrderPriority Priority { get; set; }
    public WorkOrderStatus Status { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletionSummary { get; set; }
    public string? AssetNameSnapshot { get; set; }
    public string? LocationNameSnapshot { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? SourceEventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Technician? AssignedTechnician { get; set; }
    public ICollection<TechnicianNote> Notes { get; set; } = [];

    public void AssignTechnician(Technician technician)
    {
        if (!technician.IsActive)
            throw new InvalidOperationException("Cannot assign an inactive technician.");

        if (Status != WorkOrderStatus.New)
            throw new InvalidOperationException($"Work order must be in New status to assign a technician. Current status: {Status}.");

        AssignedTechnicianId = technician.Id;
        AssignedTechnician = technician;
        AssignedAt = DateTimeOffset.UtcNow;
        Status = WorkOrderStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void StartWork()
    {
        if (Status != WorkOrderStatus.Assigned)
            throw new InvalidOperationException($"Work order must be in Assigned status to start work. Current status: {Status}.");

        Status = WorkOrderStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(string completionSummary)
    {
        if (string.IsNullOrWhiteSpace(completionSummary))
            throw new ArgumentException("Completion summary is required.", nameof(completionSummary));

        if (Status != WorkOrderStatus.InProgress)
            throw new InvalidOperationException($"Work order must be in InProgress status to complete. Current status: {Status}.");

        Status = WorkOrderStatus.Completed;
        CompletionSummary = completionSummary;
        CompletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
