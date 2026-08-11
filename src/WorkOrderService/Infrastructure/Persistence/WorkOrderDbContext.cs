using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Domain;

namespace Vision.WorkOrderService.Infrastructure.Persistence;

public class WorkOrderDbContext(DbContextOptions<WorkOrderDbContext> options)
    : DbContext(options)
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Technician> Technicians => Set<Technician>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("work_orders");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkOrderDbContext).Assembly);
    }
}
