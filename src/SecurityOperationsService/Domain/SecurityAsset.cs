namespace Vision.SecurityOperationsService.Domain;

public class SecurityAsset
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public SecurityAssetType AssetType { get; set; }
    public SecurityAssetStatus Status { get; set; }
    public string? AssetTag { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? LastServiceAt { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Location Location { get; set; } = null!;
    public ICollection<SecurityIncident> Incidents { get; set; } = [];

    public void ChangeStatus(SecurityAssetStatus newStatus)
    {
        if (Status == newStatus)
            return;

        Status = newStatus;
        StatusChangedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
