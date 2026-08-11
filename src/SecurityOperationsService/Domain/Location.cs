namespace Vision.SecurityOperationsService.Domain;

public class Location
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }
    public required string Name { get; set; }
    public string? Floor { get; set; }
    public string? Department { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Building Building { get; set; } = null!;
    public ICollection<SecurityAsset> SecurityAssets { get; set; } = [];
}
