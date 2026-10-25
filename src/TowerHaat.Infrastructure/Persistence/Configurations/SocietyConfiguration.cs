using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class SocietyConfiguration : IEntityTypeConfiguration<Society>
{
    public void Configure(EntityTypeBuilder<Society> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.City).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
    }
}
