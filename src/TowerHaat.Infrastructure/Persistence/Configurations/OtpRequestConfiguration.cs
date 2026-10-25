using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Auth;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class OtpRequestConfiguration : IEntityTypeConfiguration<OtpRequest>
{
    public void Configure(EntityTypeBuilder<OtpRequest> builder)
    {
        builder.Property(o => o.Phone).HasMaxLength(13).IsRequired();
        builder.Property(o => o.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(o => new { o.Phone, o.CreatedAt });
    }
}
