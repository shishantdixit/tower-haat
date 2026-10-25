using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class ResidentProfileConfiguration : IEntityTypeConfiguration<ResidentProfile>
{
    public void Configure(EntityTypeBuilder<ResidentProfile> builder)
    {
        builder.Property(p => p.ManualTower).HasMaxLength(50);
        builder.Property(p => p.ManualFlat).HasMaxLength(20);

        // One society per user in the MVP.
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Flat).WithMany().HasForeignKey(p => p.FlatId).OnDelete(DeleteBehavior.Restrict);
    }
}
