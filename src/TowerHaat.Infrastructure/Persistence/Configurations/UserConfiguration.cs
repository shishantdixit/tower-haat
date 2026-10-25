using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Phone).HasMaxLength(13).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(80);
        builder.HasIndex(u => u.Phone).IsUnique();
    }
}
