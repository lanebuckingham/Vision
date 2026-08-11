namespace Vision.SecurityOperationsService.Domain;

public class Building
{
    public Guid Id { get; set; }
    public Guid HospitalId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Hospital Hospital { get; set; } = null!;
    public ICollection<Location> Locations { get; set; } = [];
}
