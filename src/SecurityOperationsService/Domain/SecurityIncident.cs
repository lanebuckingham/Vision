namespace Vision.SecurityOperationsService.Domain;

public class SecurityIncident
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid? SecurityAssetId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionSummary { get; set; }
    public Guid? WorkOrderId { get; set; }

    public Location Location { get; set; } = null!;
    public SecurityAsset? SecurityAsset { get; set; }

    public void StartInvestigation()
    {
        if (Status == IncidentStatus.Resolved)
            throw new InvalidOperationException("A resolved incident cannot be moved back to Investigating.");

        if (Status == IncidentStatus.Investigating)
            return;

        Status = IncidentStatus.Investigating;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve(string resolutionSummary)
    {
        // Idempotent — if already resolved, preserve original resolution data
        if (Status == IncidentStatus.Resolved)
            return;

        if (string.IsNullOrWhiteSpace(resolutionSummary))
            throw new ArgumentException("Resolution summary is required.", nameof(resolutionSummary));

        Status = IncidentStatus.Resolved;
        ResolutionSummary = resolutionSummary;
        ResolvedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AttachWorkOrder(Guid workOrderId)
    {
        if (workOrderId == Guid.Empty)
            throw new ArgumentException("Work order ID cannot be empty.", nameof(workOrderId));

        // Idempotent — same work order can be attached again
        if (WorkOrderId == workOrderId)
            return;

        // Reject replacing an existing work order with a different one
        if (WorkOrderId is not null)
            throw new InvalidOperationException($"Incident already has work order {WorkOrderId}. Cannot replace with {workOrderId}.");

        WorkOrderId = workOrderId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool QualifiesForAutomaticWorkOrder =>
        Severity == IncidentSeverity.Critical && SecurityAssetId is not null;
}
