namespace Vision.SecurityOperationsService.Domain;

public class Hospital
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Building> Buildings { get; set; } = [];
}
