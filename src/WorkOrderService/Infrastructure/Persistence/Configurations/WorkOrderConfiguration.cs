using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Infrastructure.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(w => w.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(w => w.CompletionSummary)
            .HasMaxLength(2000);

        builder.Property(w => w.AssetNameSnapshot)
            .HasMaxLength(150);

        builder.Property(w => w.LocationNameSnapshot)
            .HasMaxLength(150);

        builder.Property(w => w.CorrelationId)
            .HasMaxLength(100);

        builder.Property(w => w.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(w => w.AssignedTechnician)
            .WithMany(t => t.WorkOrders)
            .HasForeignKey(w => w.AssignedTechnicianId)
            .OnDelete(DeleteBehavior.SetNull);

        // TechnicianNote modeled as owned child — not a top-level entity
        builder.OwnsMany(w => w.Notes, noteBuilder =>
        {
            noteBuilder.ToTable("technician_notes");
            noteBuilder.WithOwner(n => n.WorkOrder).HasForeignKey(n => n.WorkOrderId);
            noteBuilder.HasKey(n => n.Id);

            noteBuilder.Property(n => n.Content)
                .IsRequired()
                .HasMaxLength(2000);

            noteBuilder.HasIndex(n => n.WorkOrderId);
        });

        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.Priority);
        builder.HasIndex(w => w.AssignedTechnicianId);
        builder.HasIndex(w => w.SecurityAssetId);
        builder.HasIndex(w => w.CreatedAt)
            .IsDescending();
        builder.HasIndex(w => w.SecurityIncidentId).IsUnique()
            .HasFilter("security_incident_id IS NOT NULL");
        builder.HasIndex(w => w.SourceEventId).IsUnique()
            .HasFilter("source_event_id IS NOT NULL");
    }
}
