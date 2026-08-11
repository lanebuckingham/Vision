using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CredentialNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.RevocationReason)
            .HasMaxLength(500);

        builder.Property(c => c.AccessLevel)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Ignore(c => c.Status);

        builder.HasIndex(c => c.CredentialNumber).IsUnique();
        builder.HasIndex(c => c.PersonId);
        builder.HasIndex(c => c.ExpiresAt);
        builder.HasIndex(c => c.RevokedAt);
    }
}
