using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Common;
using TowerHaat.Application.Residents;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Residents;

/// <summary>Sign-up profile: name, tower and flat from the list, typed only when missing (C-01).</summary>
public sealed class ProfileTests : IDisposable
{
    private const string Phone = "+919876543210";
    private readonly AuthHarness _harness = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Society _other = new("Other Society", "Noida");
    private readonly Flat _a101;
    private readonly Flat _otherSocietyFlat;

    public ProfileTests()
    {
        using var db = _harness.Database.CreateContext(societyId: null);
        var towerA = new Tower("A") { SocietyId = Society.AceDivinoId };
        var otherTower = new Tower("A") { SocietyId = _other.Id };
        _a101 = new Flat(towerA, "101");
        _otherSocietyFlat = new Flat(otherTower, "101");
        db.Societies.Add(_other);
        db.AddRange(towerA, otherTower, _a101, _otherSocietyFlat);
        db.SaveChanges();
    }

    [Fact]
    public async Task Picking_a_listed_flat_saves_the_profile_and_issues_a_token_with_the_society()
    {
        var user = await _harness.SignInAsync(Phone);

        var saved = await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _a101.Id, null, null, true));

        Assert.Equal("Asha", saved.User.Name);
        Assert.Equal("Ace Divino", saved.User.Profile!.SocietyName);
        Assert.Equal(("A", "101", true), (saved.User.Profile.Tower, saved.User.Profile.Flat, saved.User.Profile.IsListedFlat));
        Assert.Equal(Society.AceDivinoId, _harness.Tokens.Last!.SocietyId);
    }

    [Fact]
    public async Task A_flat_missing_from_the_list_can_be_typed()
    {
        var user = await _harness.SignInAsync(Phone);

        var saved = await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, null, "C", "1204", true));

        Assert.Equal(("C", "1204", false), (saved.User.Profile!.Tower, saved.User.Profile.Flat, saved.User.Profile.IsListedFlat));
    }

    [Fact]
    public async Task Typed_text_that_matches_the_list_is_stored_as_the_listed_flat()
    {
        var user = await _harness.SignInAsync(Phone);

        var saved = await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, null, " a ", "101", true));

        Assert.Equal(_a101.Id, saved.User.Profile!.FlatId);
    }

    [Fact]
    public async Task A_flat_of_another_society_is_not_accepted()
    {
        var user = await _harness.SignInAsync(Phone);

        var error = await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _otherSocietyFlat.Id, null, null, true)));

        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Theory]
    [InlineData("A", null)]
    [InlineData(null, "101")]
    [InlineData(null, null)]
    public async Task Tower_and_flat_are_both_required(string? tower, string? flat)
    {
        var user = await _harness.SignInAsync(Phone);

        await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, null, tower, flat, true)));
    }

    [Fact]
    public async Task Picking_and_typing_a_flat_at_once_is_refused()
    {
        var user = await _harness.SignInAsync(Phone);

        await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _a101.Id, "C", "1204", true)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" A ")]
    public async Task Name_is_required(string name)
    {
        var user = await _harness.SignInAsync(Phone);

        await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest(name, Society.AceDivinoId, _a101.Id, null, null, true)));
    }

    [Fact]
    public async Task Consent_is_required_at_sign_up()
    {
        var user = await _harness.SignInAsync(Phone);

        var error = await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _a101.Id, null, null, Consent: false)));

        Assert.Contains("agree", error.Message);
        Assert.Equal(0, await _harness.Request().Db.ResidentProfiles.IgnoreQueryFilters().CountAsync(_ct));
    }

    [Fact]
    public async Task Inactive_society_cannot_be_joined()
    {
        var user = await _harness.SignInAsync(Phone);
        var (_, _, db) = _harness.Request();
        (await db.Societies.SingleAsync(s => s.Id == _other.Id, _ct)).Deactivate();
        await db.SaveChangesAsync(_ct);

        await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", _other.Id, _otherSocietyFlat.Id, null, null, true)));
    }

    [Fact]
    public async Task Profile_can_be_edited_within_the_same_society()
    {
        var user = await _harness.SignInAsync(Phone);
        await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, null, "C", "1204", true));

        var edited = await Save(
            user.User.Id, new SaveProfileRequest("Asha Rao", Society.AceDivinoId, _a101.Id, null, null, Consent: false), Society.AceDivinoId);

        Assert.Equal("Asha Rao", edited.User.Name);
        Assert.Equal(_a101.Id, edited.User.Profile!.FlatId);
    }

    [Fact]
    public async Task Moving_to_another_society_is_refused()
    {
        var user = await _harness.SignInAsync(Phone);
        await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _a101.Id, null, null, true));

        var error = await Assert.ThrowsAsync<AppException>(() => Save(
            user.User.Id, new SaveProfileRequest("Asha", _other.Id, _otherSocietyFlat.Id, null, null, true), Society.AceDivinoId));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Sign_in_after_sign_up_returns_the_profile_and_society()
    {
        var user = await _harness.SignInAsync(Phone);
        await Save(user.User.Id, new SaveProfileRequest("Asha", Society.AceDivinoId, _a101.Id, null, null, true));

        var again = await _harness.SignInAsync(Phone);

        Assert.Equal(Society.AceDivinoId, again.User.Profile!.SocietyId);
        Assert.Equal(Society.AceDivinoId, _harness.Tokens.Last!.SocietyId);
    }

    private Task<SaveProfileResponse> Save(Guid userId, SaveProfileRequest request, Guid? homeSociety = null) =>
        _harness.Request(homeSociety).Me.SaveProfileAsync(userId, request, _ct);

    public void Dispose() => _harness.Dispose();
}
