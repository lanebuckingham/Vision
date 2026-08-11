namespace Vision.WorkOrderService.Domain;

public class Technician
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public bool IsActive { get; set; }
    public string? CognitoSubject { get; set; }
    public string? Specialty { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
}
