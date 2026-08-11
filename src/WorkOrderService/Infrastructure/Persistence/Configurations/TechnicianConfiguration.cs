using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Infrastructure.Persistence.Configurations;

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(t => t.CognitoSubject)
            .HasMaxLength(128);

        builder.Property(t => t.Specialty)
            .HasMaxLength(100);

        builder.HasIndex(t => t.Email).IsUnique();
        builder.HasIndex(t => t.IsActive);
    }
}
