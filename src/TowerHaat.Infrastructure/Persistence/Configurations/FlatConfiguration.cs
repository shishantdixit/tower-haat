using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class FlatConfiguration : IEntityTypeConfiguration<Flat>
{
    public void Configure(EntityTypeBuilder<Flat> builder)
    {
        builder.Property(f => f.Number).HasMaxLength(20).IsRequired();
        builder.HasIndex(f => new { f.TowerId, f.Number }).IsUnique();
    }
}
