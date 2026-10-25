namespace TowerHaat.Domain.Orders;

public enum OrderStatus
{
    Placed = 0,
    Accepted = 1,
    Preparing = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Rejected = 5,
    Expired = 6,
    CancelledByCustomer = 7,
    CancelledByOwner = 8,
}

public enum PaymentMethod
{
    Cod = 0,
    Upi = 1,
}

public static class OrderStatuses
{
    /// <summary>States an order can still move out of.</summary>
    public static readonly OrderStatus[] Open =
        [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.OutForDelivery];

    /// <summary>Orders that took stock: everything except refused, expired and cancelled ones.</summary>
    public static readonly OrderStatus[] CountTowardsStock =
        [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.OutForDelivery, OrderStatus.Delivered];

    public static bool IsClosed(this OrderStatus status) => !Open.Contains(status);
}
