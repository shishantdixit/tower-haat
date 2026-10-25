using TowerHaat.Application.Businesses;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Businesses;

/// <summary>B-01 registration and profile, B-02 settings, business photos.</summary>
public sealed class OwnerBusinessTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    private static SaveBusinessRequest Request(
        string name = "Asha's Kitchen", Guid? type = null, Guid? category = null, string? upi = "asha@okaxis", string? fssai = null, string? whatsApp = null) =>
        new(type ?? BusinessHarness.Food, category ?? BusinessHarness.HomeKitchen, name, "Home food", upi, fssai, whatsApp);

    [Fact]
    public async Task Registering_creates_a_pending_business_in_the_owners_society()
    {
        var owner = await _h.ResidentAsync();

        var business = await _h.As(Society.AceDivinoId).Owner.RegisterAsync(owner, Request(), _h.Ct);

        Assert.Equal(BusinessStatus.Pending, business.Status);
        Assert.Equal(("Food", "Home kitchen", true), (business.BusinessTypeName, business.CategoryName, business.OrderingEnabled));
        Assert.Equal(Society.AceDivinoId, (await _h.LoadAsync(business.Id)).SocietyId);
    }

    [Fact]
    public async Task WhatsApp_number_defaults_to_the_owners_phone()
    {
        var owner = await _h.ResidentAsync();

        var withDefault = await _h.As(Society.AceDivinoId).Owner.RegisterAsync(owner, Request(), _h.Ct);
        var other = await _h.ResidentAsync();
        var withOwn = await _h.As(Society.AceDivinoId).Owner.RegisterAsync(other, Request(whatsApp: "99887 76655"), _h.Ct);

        Assert.StartsWith("+9198765", withDefault.WhatsAppPhone);
        Assert.Equal("+919988776655", withOwn.WhatsAppPhone);
    }

    [Fact]
    public async Task One_business_per_owner()
    {
        var owner = await _h.ResidentAsync();
        await _h.As(Society.AceDivinoId).Owner.RegisterAsync(owner, Request(), _h.Ct);

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Owner.RegisterAsync(owner, Request("Second"), _h.Ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Registering_needs_a_society()
    {
        var owner = await _h.ResidentAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(societyId: null).Owner.RegisterAsync(owner, Request(), _h.Ct));

        Assert.Equal(ErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public async Task Category_must_belong_to_the_chosen_type()
    {
        var owner = await _h.ResidentAsync();

        await Assert.ThrowsAsync<AppException>(() => _h.As(Society.AceDivinoId).Owner.RegisterAsync(
            owner, Request(type: BusinessHarness.Food, category: BusinessHarness.Music), _h.Ct));
    }

    [Theory]
    [InlineData("asha", null)]
    [InlineData("asha@", null)]
    [InlineData("asha@okaxis", "1234")]
    [InlineData("asha@okaxis", "1234567890123x")]
    public async Task UPI_ID_and_FSSAI_number_are_checked(string upi, string? fssai)
    {
        var owner = await _h.ResidentAsync();

        await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Owner.RegisterAsync(owner, Request(upi: upi, fssai: fssai), _h.Ct));
    }

    [Fact]
    public async Task Valid_FSSAI_number_is_stored_without_spaces()
    {
        var owner = await _h.ResidentAsync();

        var business = await _h.As(Society.AceDivinoId).Owner.RegisterAsync(
            owner, Request(fssai: "1234 5678 9012 34", upi: null), _h.Ct);

        Assert.Equal("12345678901234", business.FssaiNumber);
        Assert.Null(business.UpiId);
    }

    [Fact]
    public async Task Editing_a_rejected_business_sends_it_back_for_review()
    {
        var (owner, id) = await _h.BusinessAsync(approve: false);
        await _h.Admin().Admin.RejectAsync(id, new BusinessDecisionRequest("Add photos of your kitchen"), _h.Ct);

        var edited = await _h.As(Society.AceDivinoId).Owner.UpdateProfileAsync(owner, Request("Asha's Home Kitchen"), _h.Ct);

        Assert.Equal(BusinessStatus.Pending, edited.Status);
        Assert.Equal("Asha's Home Kitchen", edited.Name);
    }

    [Fact]
    public async Task Editing_an_active_business_keeps_it_active()
    {
        var (owner, _) = await _h.BusinessAsync();

        var edited = await _h.As(Society.AceDivinoId).Owner.UpdateProfileAsync(
            owner, Request(category: BusinessHarness.Bakery), _h.Ct);

        Assert.Equal((BusinessStatus.Active, "Bakery and desserts"), (edited.Status, edited.CategoryName));
    }

    [Fact]
    public async Task Type_cannot_change_after_registration()
    {
        var (owner, _) = await _h.BusinessAsync();

        await Assert.ThrowsAsync<AppException>(() => _h.As(Society.AceDivinoId).Owner.UpdateProfileAsync(
            owner, Request(type: BusinessHarness.Tuition, category: BusinessHarness.Music), _h.Ct));
    }

    [Fact]
    public async Task Deactivated_business_cannot_be_edited()
    {
        var (owner, id) = await _h.BusinessAsync();
        await _h.Admin().Admin.DeactivateAsync(id, new BusinessDecisionRequest("Complaints"), _h.Ct);

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Owner.SetOpenAsync(owner, false, _h.Ct));

        Assert.Equal(ErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public async Task Settings_are_saved()
    {
        var (owner, _) = await _h.BusinessAsync();

        var saved = await _h.As(Society.AceDivinoId).Owner.UpdateSettingsAsync(
            owner, new BusinessSettingsRequest("18:00", "23:30", "22:45", 150, 20), _h.Ct);

        Assert.Equal(
            new BusinessSettingsResponse("18:00", "23:30", "22:45", true, 150, 20),
            saved.Settings);
        Assert.False(saved.IsOpenNow); // 11:30 in India
    }

    [Theory]
    [InlineData("8am", "21:00", null, 0, 0)]
    [InlineData("08:00", "21:00", "22:00", 0, 0)]
    [InlineData("08:00", "21:00", null, -1, 0)]
    [InlineData("08:00", "21:00", null, 0, 10.555)]
    [InlineData("08:00", "21:00", null, 20000, 0)]
    public async Task Invalid_settings_are_refused(string opens, string closes, string? until, decimal minOrder, decimal delivery)
    {
        var (owner, _) = await _h.BusinessAsync();

        await Assert.ThrowsAsync<AppException>(() => _h.As(Society.AceDivinoId).Owner.UpdateSettingsAsync(
            owner, new BusinessSettingsRequest(opens, closes, until, minOrder, delivery), _h.Ct));
    }

    [Fact]
    public async Task Closed_switch_overrides_the_hours()
    {
        var (owner, _) = await _h.BusinessAsync();

        var closed = await _h.As(Society.AceDivinoId).Owner.SetOpenAsync(owner, false, _h.Ct);

        Assert.False(closed.IsOpenNow);
        Assert.False(closed.Settings.IsOpen);
    }

    [Fact]
    public async Task Photos_are_stored_and_removed()
    {
        var (owner, _) = await _h.BusinessAsync();
        var image = TestImages.Jpeg();

        var photo = await _h.As(Society.AceDivinoId).Owner.AddPhotoAsync(owner, image, image.Length, _h.Ct);

        Assert.StartsWith("/media/", photo.Url);
        Assert.Single(_h.Files.Keys);

        await _h.As(Society.AceDivinoId).Owner.DeletePhotoAsync(owner, photo.Id, _h.Ct);
        Assert.Empty(_h.Files.Keys);
        Assert.Empty((await _h.As(Society.AceDivinoId).Owner.GetMineAsync(owner, _h.Ct))!.Photos);
    }

    [Fact]
    public async Task Only_real_images_are_accepted()
    {
        var (owner, _) = await _h.BusinessAsync();
        var file = TestImages.NotAnImage();

        await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Owner.AddPhotoAsync(owner, file, file.Length, _h.Ct));
        Assert.Empty(_h.Files.Keys);
    }

    [Fact]
    public async Task Photo_count_is_limited()
    {
        var (owner, _) = await _h.BusinessAsync();
        for (var i = 0; i < Business.MaxPhotos; i++)
        {
            var image = TestImages.Jpeg();
            await _h.As(Society.AceDivinoId).Owner.AddPhotoAsync(owner, image, image.Length, _h.Ct);
        }

        var extra = TestImages.Jpeg();
        await Assert.ThrowsAsync<AppException>(
            () => _h.As(Society.AceDivinoId).Owner.AddPhotoAsync(owner, extra, extra.Length, _h.Ct));
    }

    [Fact]
    public async Task Owner_without_a_business_gets_nothing()
    {
        var resident = await _h.ResidentAsync();

        Assert.Null(await _h.As(Society.AceDivinoId).Owner.GetMineAsync(resident, _h.Ct));
    }

    public void Dispose() => _h.Dispose();
}
