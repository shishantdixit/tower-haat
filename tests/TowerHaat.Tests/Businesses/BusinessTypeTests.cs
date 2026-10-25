using TowerHaat.Application.Businesses;
using TowerHaat.Application.Common;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Businesses;

/// <summary>A-03: types and categories are data the admin manages.</summary>
public sealed class BusinessTypeTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    [Fact]
    public async Task Spec_types_are_seeded_with_ordering_for_food_and_products_only()
    {
        var types = await _h.Admin().Types.ListAsync(includeInactive: false, _h.Ct);

        Assert.Equal(["Food", "Products", "Tuition", "Services", "Daycare", "Other"], types.Select(t => t.Name));
        Assert.Equal(["Food", "Products"], types.Where(t => t.OrderingEnabled).Select(t => t.Name));
        Assert.All(types, t => Assert.NotEmpty(t.Categories));
    }

    [Fact]
    public async Task Admin_adds_a_type_and_category_and_can_hide_them()
    {
        var types = _h.Admin().Types;
        var plants = await types.CreateTypeAsync(new SaveBusinessTypeRequest("Plants", true, 7, true), _h.Ct);
        var category = await _h.Admin().Types.CreateCategoryAsync(plants.Id, new SaveCategoryRequest("Indoor plants", 1, true), _h.Ct);

        var listed = await _h.Admin().Types.ListAsync(includeInactive: false, _h.Ct);
        Assert.Equal("Indoor plants", Assert.Single(listed.Single(t => t.Id == plants.Id).Categories).Name);

        await _h.Admin().Types.UpdateCategoryAsync(category.Id, new SaveCategoryRequest("Indoor plants", 1, false), _h.Ct);
        await _h.Admin().Types.UpdateTypeAsync(plants.Id, new SaveBusinessTypeRequest("Plants", true, 7, false), _h.Ct);

        Assert.DoesNotContain(await _h.Admin().Types.ListAsync(includeInactive: false, _h.Ct), t => t.Id == plants.Id);
        var all = await _h.Admin().Types.ListAsync(includeInactive: true, _h.Ct);
        Assert.False(all.Single(t => t.Id == plants.Id).Categories.Single().IsActive);
    }

    [Fact]
    public async Task Names_are_unique()
    {
        await Assert.ThrowsAsync<AppException>(
            () => _h.Admin().Types.CreateTypeAsync(new SaveBusinessTypeRequest("food", true, 9, true), _h.Ct));
        await Assert.ThrowsAsync<AppException>(() => _h.Admin().Types.CreateCategoryAsync(
            BusinessHarness.Food, new SaveCategoryRequest("HOME KITCHEN", 9, true), _h.Ct));
    }

    [Fact]
    public async Task Hidden_category_cannot_be_chosen_at_registration()
    {
        await _h.Admin().Types.UpdateCategoryAsync(BusinessHarness.HomeKitchen, new SaveCategoryRequest("Home kitchen", 1, false), _h.Ct);
        var owner = await _h.ResidentAsync();

        await Assert.ThrowsAsync<AppException>(() => _h.As(TowerHaat.Domain.Societies.Society.AceDivinoId).Owner.RegisterAsync(
            owner,
            new SaveBusinessRequest(BusinessHarness.Food, BusinessHarness.HomeKitchen, "K", null, null, null, null),
            _h.Ct));
    }

    public void Dispose() => _h.Dispose();
}
