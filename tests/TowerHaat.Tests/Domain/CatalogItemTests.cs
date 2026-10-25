using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Catalog;

namespace TowerHaat.Tests.Domain;

/// <summary>C-04: sold out when unavailable today or the daily limit is reached.</summary>
public sealed class CatalogItemTests
{
    private static CatalogItem Item(int? dailyLimit)
    {
        var type = new BusinessType("Food", true, 1);
        var category = new Category(type.Id, "Home kitchen", 1);
        var business = new Business(Guid.CreateVersion7(), type, category, new BusinessProfile("K", null, null, null, "+919876543210"), DateTime.UtcNow);
        return new CatalogItem(business, new CatalogItemDetails("Thali", null, 120, true, dailyLimit, false, 0), DateTime.UtcNow);
    }

    [Fact]
    public void Available_item_without_a_limit_never_sells_out()
    {
        var item = Item(dailyLimit: null);

        Assert.False(item.IsSoldOut(soldToday: 500));
        Assert.Null(item.RemainingToday(500));
    }

    [Fact]
    public void Daily_limit_reached_means_sold_out()
    {
        var item = Item(dailyLimit: 10);

        Assert.False(item.IsSoldOut(9));
        Assert.Equal(1, item.RemainingToday(9));
        Assert.True(item.IsSoldOut(10));
        Assert.Equal(0, item.RemainingToday(12));
    }

    [Fact]
    public void Switched_off_for_today_means_sold_out()
    {
        var item = Item(dailyLimit: null);

        item.SetAvailableToday(false);

        Assert.True(item.IsSoldOut(0));
    }

    [Fact]
    public void Price_must_be_positive()
    {
        var item = Item(dailyLimit: null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => item.Update(new CatalogItemDetails("Thali", null, 0, true, null, false, 0)));
    }
}
