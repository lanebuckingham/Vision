using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Configurations;

public class SecurityIncidentConfiguration : IEntityTypeConfiguration<SecurityIncident>
{
    public void Configure(EntityTypeBuilder<SecurityIncident> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(i => i.ResolutionSummary)
            .HasMaxLength(2000);

        builder.Property(i => i.Severity)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne(i => i.Location)
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.LocationId);
        builder.HasIndex(i => i.SecurityAssetId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.Severity);
        builder.HasIndex(i => new { i.Status, i.Severity });
        builder.HasIndex(i => i.CreatedAt).IsDescending();
        builder.HasIndex(i => i.WorkOrderId)
            .IsUnique()
            .HasFilter("work_order_id IS NOT NULL");
    }
}
