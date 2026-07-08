using System.Reflection;
using FactoryPulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryPulse.Infrastructure.Persistence;

public class FactoryPulseDbContext : DbContext
{
    public FactoryPulseDbContext(DbContextOptions<FactoryPulseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
