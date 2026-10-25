using TowerHaat.Application.Businesses;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Businesses;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Businesses;

/// <summary>A-02: approve, reject, deactivate with a note.</summary>
public sealed class BusinessAdminTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    [Fact]
    public async Task Queue_lists_pending_businesses_of_every_society_with_the_owner()
    {
        var (_, ace) = await _h.BusinessAsync("Ace Kitchen", approve: false);
        var (_, other) = await _h.BusinessAsync("Other Kitchen", _h.OtherSociety.Id, approve: false);
        await _h.BusinessAsync("Approved Kitchen");

        var pending = await _h.Admin().Admin.ListAsync(BusinessStatus.Pending, _h.Ct);

        Assert.Equal([ace, other], pending.Select(b => b.Id));
        Assert.Equal(["Ace Divino", "Other Society"], pending.Select(b => b.SocietyName));
        Assert.All(pending, b => Assert.StartsWith("+9198765", b.OwnerPhone));
    }

    [Fact]
    public async Task Approving_makes_the_business_active_with_an_optional_note()
    {
        var (_, id) = await _h.BusinessAsync(approve: false);

        await _h.Admin().Admin.ApproveAsync(id, new BusinessDecisionRequest("Welcome!"), _h.Ct);

        var business = await _h.LoadAsync(id);
        Assert.Equal((BusinessStatus.Active, "Welcome!"), (business.Status, business.StatusNote));
        Assert.NotNull(business.ApprovedAt);
    }

    [Fact]
    public async Task Approving_works_for_any_society()
    {
        var (_, id) = await _h.BusinessAsync(societyId: _h.OtherSociety.Id, approve: false);

        await _h.Admin().Admin.ApproveAsync(id, new BusinessDecisionRequest(null), _h.Ct);

        Assert.Equal(BusinessStatus.Active, (await _h.LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Rejecting_and_deactivating_need_a_note()
    {
        var (_, pending) = await _h.BusinessAsync(approve: false);
        var (_, active) = await _h.BusinessAsync("Active");

        await Assert.ThrowsAsync<AppException>(() => _h.Admin().Admin.RejectAsync(pending, new BusinessDecisionRequest(" "), _h.Ct));
        await Assert.ThrowsAsync<AppException>(() => _h.Admin().Admin.DeactivateAsync(active, new BusinessDecisionRequest(null), _h.Ct));

        await _h.Admin().Admin.RejectAsync(pending, new BusinessDecisionRequest("Blurry photos"), _h.Ct);
        await _h.Admin().Admin.DeactivateAsync(active, new BusinessDecisionRequest("Owner moved out"), _h.Ct);

        Assert.Equal((BusinessStatus.Rejected, "Blurry photos"), ((await _h.LoadAsync(pending)).Status, (await _h.LoadAsync(pending)).StatusNote));
        Assert.Equal(BusinessStatus.Deactivated, (await _h.LoadAsync(active)).Status);
    }

    [Fact]
    public async Task Only_pending_businesses_can_be_rejected()
    {
        var (_, active) = await _h.BusinessAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.Admin().Admin.RejectAsync(active, new BusinessDecisionRequest("No"), _h.Ct));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    [Fact]
    public async Task Deactivated_business_can_be_reactivated()
    {
        var (_, id) = await _h.BusinessAsync();
        await _h.Admin().Admin.DeactivateAsync(id, new BusinessDecisionRequest("Paused"), _h.Ct);

        await _h.Admin().Admin.ApproveAsync(id, new BusinessDecisionRequest(null), _h.Ct);

        Assert.Equal(BusinessStatus.Active, (await _h.LoadAsync(id)).Status);
    }

    [Fact]
    public async Task Unknown_business_is_not_found()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _h.Admin().Admin.ApproveAsync(Guid.CreateVersion7(), new BusinessDecisionRequest(null), _h.Ct));

        Assert.Equal(ErrorKind.NotFound, error.Kind);
    }

    public void Dispose() => _h.Dispose();
}
