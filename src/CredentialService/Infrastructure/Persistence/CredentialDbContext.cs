using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Domain;

namespace Vision.CredentialService.Infrastructure.Persistence;

public class CredentialDbContext(DbContextOptions<CredentialDbContext> options)
    : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Credential> Credentials => Set<Credential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("credentials");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CredentialDbContext).Assembly);
    }
}
