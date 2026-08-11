namespace Vision.WorkOrderService.Domain;

public class TechnicianNote
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public WorkOrder WorkOrder { get; set; } = null!;
}
