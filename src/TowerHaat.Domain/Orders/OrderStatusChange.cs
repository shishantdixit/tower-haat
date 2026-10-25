using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Orders;

/// <summary>One step of an order's timeline (C-07).</summary>
public class OrderStatusChange : Entity, ITenantEntity
{
    private OrderStatusChange()
    {
    }

    internal OrderStatusChange(Order order, OrderStatus status, Guid? changedByUserId, string? reason, DateTime at)
    {
        SocietyId = order.SocietyId;
        OrderId = order.Id;
        Status = status;
        ChangedByUserId = changedByUserId;
        Reason = reason;
        At = at;
    }

    public Guid SocietyId { get; set; }

    public Guid OrderId { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>Null when the system changed it (expiry).</summary>
    public Guid? ChangedByUserId { get; private set; }

    public string? Reason { get; private set; }

    public DateTime At { get; private set; }
}
