using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Users;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(80).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(1000);
        builder.Property(b => b.UpiId).HasMaxLength(320);
        builder.Property(b => b.FssaiNumber).HasMaxLength(14);
        builder.Property(b => b.WhatsAppPhone).HasMaxLength(13).IsRequired();
        builder.Property(b => b.StatusNote).HasMaxLength(500);

        builder.ComplexProperty(b => b.Hours, hours =>
        {
            hours.Property(h => h.OpensAt).HasColumnName("OpensAt");
            hours.Property(h => h.ClosesAt).HasColumnName("ClosesAt");
            hours.Property(h => h.AcceptOrdersUntil).HasColumnName("AcceptOrdersUntil");
        });

        builder.HasIndex(b => b.OwnerUserId);
        builder.HasIndex(b => new { b.SocietyId, b.Status });
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BusinessType>().WithMany().HasForeignKey(b => b.BusinessTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(b => b.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Photos).WithOne().HasForeignKey(p => p.BusinessId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.Photos).HasField("_photos");
    }
}

internal sealed class BusinessPhotoConfiguration : IEntityTypeConfiguration<BusinessPhoto>
{
    public void Configure(EntityTypeBuilder<BusinessPhoto> builder)
    {
        builder.Property(p => p.FileKey).HasMaxLength(100).IsRequired();
    }
}
