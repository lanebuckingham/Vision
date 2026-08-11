using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.EmployeeNumber)
            .HasMaxLength(50);

        builder.Property(p => p.Email)
            .HasMaxLength(254);

        builder.Property(p => p.Department)
            .HasMaxLength(100);

        builder.Property(p => p.JobTitle)
            .HasMaxLength(100);

        builder.Property(p => p.PersonType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(p => p.Credentials)
            .WithOne(c => c.Person)
            .HasForeignKey(c => c.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.EmployeeNumber).IsUnique()
            .HasFilter("employee_number IS NOT NULL");
        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => new { p.LastName, p.FirstName });
    }
}
