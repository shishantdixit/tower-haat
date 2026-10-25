using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Notifications;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(s => s.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(s => s.EndpointHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.EndpointHash).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
