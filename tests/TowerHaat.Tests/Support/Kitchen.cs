using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Orders;
using TowerHaat.Domain.Orders;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Tests.Support;

/// <summary>An approved kitchen with a small menu, and helpers to order from it.</summary>
public sealed class Kitchen
{
    private Kitchen(BusinessHarness harness, Guid ownerId, Guid businessId)
    {
        Harness = harness;
        OwnerId = ownerId;
        BusinessId = businessId;
    }

    public BusinessHarness Harness { get; }

    public Guid OwnerId { get; }

    public Guid BusinessId { get; }

    /// <summary>₹120, limit 20 a day.</summary>
    public Guid Thali { get; private set; }

    /// <summary>₹60, no limit.</summary>
    public Guid Paratha { get; private set; }

    /// <summary>Asha's Kitchen in Ace Divino: open 08:00 to 21:00, orders until 20:00, minimum ₹100, delivery ₹20.</summary>
    public static async Task<Kitchen> OpenAsync(BusinessHarness h, string name = "Asha's Kitchen", Guid? societyId = null)
    {
        var society = societyId ?? Society.AceDivinoId;
        var (owner, business) = await h.BusinessAsync(name, society);
        var kitchen = new Kitchen(h, owner, business);
        var services = h.As(society);
        await services.Owner.UpdateSettingsAsync(owner, new BusinessSettingsRequest("08:00", "21:00", "20:00", 100, 20), h.Ct);
        kitchen.Thali = (await h.As(society).Catalog.CreateAsync(
            owner, new SaveItemRequest("Veg thali", null, 120, true, 20, true, false, 1), h.Ct)).Id;
        kitchen.Paratha = (await h.As(society).Catalog.CreateAsync(
            owner, new SaveItemRequest("Aloo paratha", null, 60, true, null, true, false, 2), h.Ct)).Id;
        return kitchen;
    }

    public PlaceOrderRequest Request(
        IReadOnlyList<(Guid Item, int Quantity)> lines,
        PaymentMethod payment = PaymentMethod.Cod,
        string? day = null,
        string? at = null,
        Guid? key = null,
        string? note = null) =>
        new(BusinessId, lines.Select(l => new OrderLineRequest(l.Item, l.Quantity)).ToList(), note, day, at, payment, key ?? Guid.NewGuid());

    public Task<OrderResponse> OrderAsync(Guid customerId, params (Guid Item, int Quantity)[] lines) =>
        Harness.As(Society.AceDivinoId).Orders.PlaceAsync(customerId, Request(lines), Harness.Ct);

    public OwnerOrderService Owner => Harness.As(Society.AceDivinoId).OwnerOrders;
}
