using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasMany(b => b.Locations)
            .WithOne(l => l.Building)
            .HasForeignKey(l => l.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.HospitalId);
        builder.HasIndex(b => new { b.HospitalId, b.Name }).IsUnique();
    }
}
