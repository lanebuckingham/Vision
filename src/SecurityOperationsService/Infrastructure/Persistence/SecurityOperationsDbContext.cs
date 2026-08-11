using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.Domain;

namespace Vision.SecurityOperationsService.Infrastructure.Persistence;

public class SecurityOperationsDbContext(DbContextOptions<SecurityOperationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<SecurityAsset> SecurityAssets => Set<SecurityAsset>();
    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("security_operations");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SecurityOperationsDbContext).Assembly);
    }
}
