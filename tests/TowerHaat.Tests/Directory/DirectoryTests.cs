using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Directory;

/// <summary>C-02 home, C-03 business page, C-04 items and sold out.</summary>
public sealed class DirectoryTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    [Fact]
    public async Task Residents_see_active_businesses_of_their_own_society_only()
    {
        await _h.BusinessAsync("Asha's Kitchen");
        await _h.BusinessAsync("Pending Kitchen", approve: false);
        await _h.BusinessAsync("Other Society Kitchen", _h.OtherSociety.Id);
        await _h.ResidentAsync();

        var directory = await _h.As(Society.AceDivinoId).Directory.ListAsync(_h.Ct);

        Assert.Equal(["Asha's Kitchen"], directory.Businesses.Select(b => b.Name));
        Assert.Equal([("Home kitchen", 1)], directory.Categories.Select(c => (c.Name, c.Count)));
    }

    [Fact]
    public async Task Open_businesses_come_first_with_a_badge()
    {
        var (closedOwner, _) = await _h.BusinessAsync("A Closed Kitchen");
        await _h.BusinessAsync("Z Open Kitchen");
        await _h.As(Society.AceDivinoId).Owner.SetOpenAsync(closedOwner, false, _h.Ct);

        var cards = (await _h.As(Society.AceDivinoId).Directory.ListAsync(_h.Ct)).Businesses;

        Assert.Equal([("Z Open Kitchen", true), ("A Closed Kitchen", false)], cards.Select(c => (c.Name, c.IsOpenNow)));
    }

    [Fact]
    public async Task Open_state_follows_the_hours_in_indian_time()
    {
        var (owner, id) = await _h.BusinessAsync();
        await _h.As(Society.AceDivinoId).Owner.UpdateSettingsAsync(
            owner, new BusinessSettingsRequest("12:00", "22:00", "21:00", 0, 0), _h.Ct);

        // 11:30 in India.
        Assert.False((await _h.As(Society.AceDivinoId).Directory.GetAsync(owner, id, _h.Ct)).IsOpenNow);

        _h.Time.Advance(TimeSpan.FromHours(1)); // 12:30
        var page = await _h.As(Society.AceDivinoId).Directory.GetAsync(owner, id, _h.Ct);
        Assert.True(page.IsOpenNow && page.IsAcceptingOrders);

        _h.Time.Advance(TimeSpan.FromHours(9)); // 21:30: open, past the last order time
        page = await _h.As(Society.AceDivinoId).Directory.GetAsync(owner, id, _h.Ct);
        Assert.True(page.IsOpenNow);
        Assert.False(page.IsAcceptingOrders);
    }

    [Fact]
    public async Task Business_page_shows_visible_items_with_sold_out_state()
    {
        var (owner, id) = await _h.BusinessAsync();
        var catalog = _h.As(Society.AceDivinoId).Catalog;
        await catalog.CreateAsync(owner, new SaveItemRequest("Thali", null, 120, true, null, true, false, 1), _h.Ct);
        await catalog.CreateAsync(owner, new SaveItemRequest("Chicken curry", null, 180, false, null, false, false, 2), _h.Ct);
        await catalog.CreateAsync(owner, new SaveItemRequest("Secret dish", null, 99, true, null, true, true, 3), _h.Ct);
        var customer = await _h.ResidentAsync();

        var page = await _h.As(Society.AceDivinoId).Directory.GetAsync(customer, id, _h.Ct);

        Assert.Equal([("Thali", false, (bool?)true), ("Chicken curry", true, false)], page.Items.Select(i => (i.Name, i.IsSoldOut, i.IsVeg)));
        Assert.StartsWith("https://wa.me/9198765", page.WhatsAppUrl);
        Assert.False(page.IsOwnerPreview);
    }

    [Fact]
    public async Task Pending_business_is_hidden_from_customers_but_the_owner_can_preview_it()
    {
        var (owner, id) = await _h.BusinessAsync(approve: false);
        var customer = await _h.ResidentAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Directory.GetAsync(customer, id, _h.Ct));
        var preview = await _h.As(Society.AceDivinoId).Directory.GetAsync(owner, id, _h.Ct);

        Assert.Equal(ErrorKind.NotFound, error.Kind);
        Assert.True(preview.IsOwnerPreview);
    }

    [Fact]
    public async Task Business_of_another_society_is_not_found()
    {
        var (_, id) = await _h.BusinessAsync(societyId: _h.OtherSociety.Id);
        var customer = await _h.ResidentAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Directory.GetAsync(customer, id, _h.Ct));

        Assert.Equal(ErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task Listing_only_business_has_no_menu()
    {
        var (owner, id) = await _h.BusinessAsync("Riya's Music Class", typeId: BusinessHarness.Tuition, categoryId: BusinessHarness.Music);

        var page = await _h.As(Society.AceDivinoId).Directory.GetAsync(owner, id, _h.Ct);

        Assert.False(page.OrderingEnabled);
        Assert.False(page.IsAcceptingOrders);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Directory_needs_a_society()
    {
        await Assert.ThrowsAsync<AppException>(() => _h.As(societyId: null).Directory.ListAsync(_h.Ct));
    }

    public void Dispose() => _h.Dispose();
}
