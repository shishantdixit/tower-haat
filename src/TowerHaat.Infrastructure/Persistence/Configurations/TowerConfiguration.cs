using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class TowerConfiguration : IEntityTypeConfiguration<Tower>
{
    public void Configure(EntityTypeBuilder<Tower> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => new { t.SocietyId, t.Name }).IsUnique();
        builder.HasMany(t => t.Flats).WithOne(f => f.Tower).HasForeignKey(f => f.TowerId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.Flats).HasField("_flats");
    }
}
