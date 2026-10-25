using TowerHaat.Application.Admin;
using TowerHaat.Application.Payments;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Orders;

public sealed class PilotStatsTests : IDisposable
{
    private readonly BusinessHarness _h = new();

    [Fact]
    public async Task Counts_orders_per_indian_day_by_outcome_and_per_business()
    {
        var k = await Kitchen.OpenAsync(_h);
        var other = await Kitchen.OpenAsync(_h, "Other Kitchen", _h.OtherSociety.Id);
        var customer = await _h.ResidentAsync();
        var outsider = await _h.ResidentAsync(_h.OtherSociety.Id);

        var delivered = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.AcceptAsync(k.OwnerId, delivered.Id, _h.Ct);
        for (var i = 0; i < 3; i++)
        {
            await k.Owner.AdvanceAsync(k.OwnerId, delivered.Id, _h.Ct);
        }

        var rejected = await k.OrderAsync(customer, (k.Thali, 1));
        await k.Owner.RejectAsync(k.OwnerId, rejected.Id, new("No"), _h.Ct);
        var disputed = await _h.As(Society.AceDivinoId).Orders.PlaceAsync(customer, k.Request([(k.Thali, 1)], PaymentMethod.Upi), _h.Ct);
        await k.Owner.AcceptAsync(k.OwnerId, disputed.Id, _h.Ct);
        await _h.As(Society.AceDivinoId).Orders.MarkPaidAsync(customer, disputed.Id, new(null), _h.Ct);
        await k.Owner.UpdatePaymentAsync(k.OwnerId, disputed.Id, new(PaymentAction.Dispute, null), _h.Ct);
        await _h.As(_h.OtherSociety.Id).Orders.PlaceAsync(outsider, other.Request([(other.Thali, 1)]), _h.Ct);

        var stats = await new PilotStatsService(_h.Admin().Db, _h.Time).GetAsync(7, _h.Ct);

        Assert.Equal(7, stats.Days.Count);
        Assert.Equal(new DateOnly(2026, 10, 25), stats.Days[0].Date);
        Assert.Equal(new DayStats(new DateOnly(2026, 10, 25), 4, 1, 1, 0, 0, 1), stats.Days[0]);
        Assert.Equal((2, 2), (stats.ActiveBusinesses, stats.CustomersInPeriod));
        var asha = stats.Businesses.Single(b => b.Name == "Asha's Kitchen");
        Assert.Equal(new BusinessStats("Asha's Kitchen", 3, 1, 1, 140), asha);
    }

    public void Dispose() => _h.Dispose();
}
