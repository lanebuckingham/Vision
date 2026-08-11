using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(l => l.Floor)
            .HasMaxLength(20);

        builder.Property(l => l.Department)
            .HasMaxLength(100);

        builder.Property(l => l.Description)
            .HasMaxLength(500);

        builder.HasMany(l => l.SecurityAssets)
            .WithOne(a => a.Location)
            .HasForeignKey(a => a.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.BuildingId);
        builder.HasIndex(l => new { l.BuildingId, l.Name }).IsUnique();
    }
}
