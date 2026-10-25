using TowerHaat.Application.Catalog;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Catalog;

/// <summary>B-03: the owner's catalog.</summary>
public sealed class CatalogTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private static SaveItemRequest Item(
        string name = "Veg thali", decimal price = 120, bool? isVeg = true, int? dailyLimit = null, bool available = true, bool hidden = false) =>
        new(name, "Dal, sabzi, 4 rotis, rice", price, isVeg, dailyLimit, available, hidden, 0);

    private CatalogService Catalog => _h.As(Society.AceDivinoId).Catalog;

    [Fact]
    public async Task Owner_adds_edits_and_removes_items()
    {
        var (owner, _) = await _h.BusinessAsync();

        var thali = await Catalog.CreateAsync(owner, Item(), _h.Ct);
        await Catalog.UpdateAsync(owner, thali.Id, Item("Special thali", 150, dailyLimit: 20), _h.Ct);
        var paneer = await Catalog.CreateAsync(owner, Item("Paneer roll", 80), _h.Ct);
        await Catalog.DeleteAsync(owner, paneer.Id, _h.Ct);

        var item = Assert.Single(await Catalog.ListAsync(owner, _h.Ct));
        Assert.Equal(("Special thali", 150m, 20), (item.Name, item.Price, item.DailyLimit));
    }

    [Fact]
    public async Task Available_today_switch_marks_the_item_sold_out()
    {
        var (owner, _) = await _h.BusinessAsync();
        var thali = await Catalog.CreateAsync(owner, Item(), _h.Ct);

        var off = await Catalog.SetAvailabilityAsync(owner, thali.Id, false, _h.Ct);

        Assert.False(off.IsAvailableToday);
        Assert.True(off.IsSoldOut);
    }

    [Fact]
    public async Task Daily_limit_of_zero_is_sold_out()
    {
        var (owner, _) = await _h.BusinessAsync();

        var item = await Catalog.CreateAsync(owner, Item(dailyLimit: 0), _h.Ct);

        Assert.True(item.IsSoldOut);
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Thali", 0)]
    [InlineData("Thali", -5)]
    [InlineData("Thali", 99.999)]
    [InlineData("Thali", 200000)]
    public async Task Invalid_items_are_refused(string name, decimal price)
    {
        var (owner, _) = await _h.BusinessAsync();

        await Assert.ThrowsAsync<AppException>(() => Catalog.CreateAsync(owner, Item(name, price), _h.Ct));
    }

    [Fact]
    public async Task Pending_business_can_prepare_its_catalog()
    {
        var (owner, _) = await _h.BusinessAsync(approve: false);

        await Catalog.CreateAsync(owner, Item(), _h.Ct);

        Assert.Single(await Catalog.ListAsync(owner, _h.Ct));
    }

    [Fact]
    public async Task Listing_only_types_have_no_catalog()
    {
        var (owner, _) = await _h.BusinessAsync(typeId: BusinessHarness.Tuition, categoryId: BusinessHarness.Music);

        await Assert.ThrowsAsync<AppException>(() => Catalog.CreateAsync(owner, Item(), _h.Ct));
    }

    [Fact]
    public async Task An_owner_cannot_touch_another_owners_items()
    {
        var (asha, _) = await _h.BusinessAsync("Asha's Kitchen");
        var (ravi, _) = await _h.BusinessAsync("Ravi's Bakes");
        var thali = await Catalog.CreateAsync(asha, Item(), _h.Ct);

        var error = await Assert.ThrowsAsync<AppException>(
            () => Catalog.UpdateAsync(ravi, thali.Id, Item("Mine now"), _h.Ct));

        Assert.Equal(ErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task Item_photo_is_replaced_and_removed()
    {
        var (owner, _) = await _h.BusinessAsync();
        var thali = await Catalog.CreateAsync(owner, Item(), _h.Ct);

        var first = TestImages.Jpeg();
        await Catalog.SetPhotoAsync(owner, thali.Id, first, first.Length, _h.Ct);
        var second = TestImages.Jpeg();
        var replaced = await Catalog.SetPhotoAsync(owner, thali.Id, second, second.Length, _h.Ct);

        Assert.Single(_h.Files.Keys);
        Assert.NotNull(replaced.PhotoUrl);

        var removed = await Catalog.RemovePhotoAsync(owner, thali.Id, _h.Ct);
        Assert.Null(removed.PhotoUrl);
        Assert.Empty(_h.Files.Keys);
    }

    public void Dispose() => _h.Dispose();
}
