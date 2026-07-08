using FactoryPulse.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FactoryPulse.Infrastructure.Configurations;

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrder");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();

        builder.Property(o => o.ProductName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(o => o.Machine)
            .WithMany()
            .HasForeignKey(o => o.MachineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
