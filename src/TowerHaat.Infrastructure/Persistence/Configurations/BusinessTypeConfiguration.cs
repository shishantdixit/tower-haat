using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TowerHaat.Domain.Businesses;

namespace TowerHaat.Infrastructure.Persistence.Configurations;

internal sealed class BusinessTypeConfiguration : IEntityTypeConfiguration<BusinessType>
{
    public void Configure(EntityTypeBuilder<BusinessType> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasData(BusinessTypeSeed.Types.Select(t => new
        {
            t.Id,
            t.Name,
            t.OrderingEnabled,
            t.SortOrder,
            IsActive = true,
        }));
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => new { c.BusinessTypeId, c.Name }).IsUnique();
        builder.HasOne<BusinessType>().WithMany().HasForeignKey(c => c.BusinessTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(BusinessTypeSeed.Categories.Select(c => new
        {
            c.Id,
            c.BusinessTypeId,
            c.Name,
            c.SortOrder,
            IsActive = true,
        }));
    }
}

/// <summary>Starting types and categories from the spec; admins change them from then on (A-03).</summary>
internal static class BusinessTypeSeed
{
    public sealed record TypeSeed(Guid Id, string Name, bool OrderingEnabled, int SortOrder, string[] Categories);

    public sealed record CategorySeed(Guid Id, Guid BusinessTypeId, string Name, int SortOrder);

    public static readonly TypeSeed[] Types =
    [
        new(Id(1, 1), "Food", true, 1, ["Home kitchen", "Tiffin and meals", "Bakery and desserts", "Snacks and sweets"]),
        new(Id(1, 2), "Products", true, 2, ["Clothing and accessories", "Home and decor", "Homemade products"]),
        new(Id(1, 3), "Tuition", false, 3, ["Academic", "Music", "Dance", "Art and craft"]),
        new(Id(1, 4), "Services", false, 4, ["Beauty and wellness", "Tailoring", "Repairs", "Fitness and yoga"]),
        new(Id(1, 5), "Daycare", false, 5, ["Daycare"]),
        new(Id(1, 6), "Other", false, 6, ["Other"]),
    ];

    public static readonly CategorySeed[] Categories = Types
        .SelectMany(t => t.Categories.Select((name, i) => new CategorySeed(
            Id(2, (t.SortOrder * 100) + i + 1), t.Id, name, i + 1)))
        .ToArray();

    // Fixed ids so the seed stays the same across migrations.
    private static Guid Id(int kind, int n) => new($"01920000-0000-7000-800{kind}-{n:D12}");
}
