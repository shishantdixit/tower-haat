using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.Property(i => i.Name).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.PhotoKey).HasMaxLength(100);
        builder.HasIndex(i => i.BusinessId);
        builder.HasOne<Business>().WithMany().HasForeignKey(i => i.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
