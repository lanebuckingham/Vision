using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Configurations;

public class SecurityAssetConfiguration : IEntityTypeConfiguration<SecurityAsset>
{
    public void Configure(EntityTypeBuilder<SecurityAsset> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.AssetTag)
            .HasMaxLength(50);

        builder.Property(a => a.Manufacturer)
            .HasMaxLength(100);

        builder.Property(a => a.Model)
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .HasMaxLength(500);

        builder.Property(a => a.AssetType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasMany(a => a.Incidents)
            .WithOne(i => i.SecurityAsset)
            .HasForeignKey(i => i.SecurityAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.LocationId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.AssetType);
        builder.HasIndex(a => new { a.Status, a.AssetType });
        builder.HasIndex(a => a.AssetTag)
            .IsUnique()
            .HasFilter("asset_tag IS NOT NULL");
    }
}
