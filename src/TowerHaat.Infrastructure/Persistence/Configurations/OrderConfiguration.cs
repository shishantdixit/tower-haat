using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Note).HasMaxLength(300);
        builder.Property(o => o.CustomerName).HasMaxLength(80).IsRequired();
        builder.Property(o => o.CustomerPhone).HasMaxLength(13).IsRequired();
        builder.Property(o => o.Tower).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Flat).HasMaxLength(20).IsRequired();

        // Order numbers are sequential per business; a repeated checkout returns the same order.
        builder.HasIndex(o => new { o.BusinessId, o.OrderNo }).IsUnique();
        builder.HasIndex(o => new { o.CustomerUserId, o.IdempotencyKey }).IsUnique();
        builder.HasIndex(o => new { o.BusinessId, o.Status });
        builder.HasIndex(o => new { o.Status, o.PlacedAt });

        builder.HasOne<Business>().WithMany().HasForeignKey(o => o.BusinessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.CustomerUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).HasField("_items");
        builder.HasOne(o => o.Payment).WithOne().HasForeignKey<Payment>(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.History).HasField("_history");
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        // No foreign key to the catalog: the snapshot stays valid after an item is edited or deleted.
        builder.Property(i => i.NameSnapshot).HasMaxLength(100).IsRequired();
        builder.HasIndex(i => i.CatalogItemId);
    }
}

internal sealed class OrderStatusChangeConfiguration : IEntityTypeConfiguration<OrderStatusChange>
{
    public void Configure(EntityTypeBuilder<OrderStatusChange> builder)
    {
        builder.Property(h => h.Reason).HasMaxLength(300);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Utr).HasMaxLength(35);
        builder.Property(p => p.Note).HasMaxLength(300);
    }
}
