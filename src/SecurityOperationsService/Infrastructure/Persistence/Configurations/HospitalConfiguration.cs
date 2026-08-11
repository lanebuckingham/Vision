using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Configurations;

public class HospitalConfiguration : IEntityTypeConfiguration<Hospital>
{
    public void Configure(EntityTypeBuilder<Hospital> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(h => h.Code)
            .HasMaxLength(50);

        builder.HasMany(h => h.Buildings)
            .WithOne(b => b.Hospital)
            .HasForeignKey(b => b.HospitalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
