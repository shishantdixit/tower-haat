using TowerHaat.Domain.Businesses;
using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Orders;

/// <summary>
/// One order from one customer to one business (C-06). Name, price and delivery address are
/// copied in, so later catalog or profile edits never change it. The status follows
/// Placed → Accepted → Preparing → OutForDelivery → Delivered, or ends Rejected, Expired,
/// CancelledByCustomer (only while Placed) or CancelledByOwner (with a reason).
/// </summary>
public class Order : Entity, ITenantEntity
{
    /// <summary>A Placed order not accepted within this time expires.</summary>
    public static readonly TimeSpan AcceptWithin = TimeSpan.FromMinutes(30);

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderStatusChange> _history = [];

    private Order()
    {
    }

    public Order(
        Business business,
        Guid customerUserId,
        int orderNo,
        OrderCustomer customer,
        IReadOnlyList<(Catalog.CatalogItem Item, int Quantity)> lines,
        DateTime? preferredTime,
        string? note,
        PaymentMethod paymentMethod,
        Guid idempotencyKey,
        DateTime now)
    {
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("An order needs at least one item.");
        }

        SocietyId = business.SocietyId;
        BusinessId = business.Id;
        CustomerUserId = customerUserId;
        OrderNo = orderNo;
        CustomerName = customer.Name;
        CustomerPhone = customer.Phone;
        Tower = customer.Tower;
        Flat = customer.Flat;
        PreferredTime = preferredTime;
        Note = note;
        PaymentMethod = paymentMethod;
        IdempotencyKey = idempotencyKey;
        PlacedAt = now;

        foreach (var (item, quantity) in lines)
        {
            _items.Add(new OrderItem(this, item, quantity));
        }

        Subtotal = _items.Sum(i => i.LineTotal);
        DeliveryCharge = business.DeliveryCharge;
        Total = Subtotal + DeliveryCharge;
        Record(OrderStatus.Placed, customerUserId, reason: null, now);
    }

    public Guid SocietyId { get; set; }

    public Guid BusinessId { get; private set; }

    public Guid CustomerUserId { get; private set; }

    /// <summary>Sequential per business, shown as #12.</summary>
    public int OrderNo { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime PlacedAt { get; private set; }

    public DateTime StatusChangedAt { get; private set; }

    /// <summary>Delivery time the customer asked for (UTC); null means as soon as possible.</summary>
    public DateTime? PreferredTime { get; private set; }

    public string? Note { get; private set; }

    public string CustomerName { get; private set; } = null!;

    public string CustomerPhone { get; private set; } = null!;

    public string Tower { get; private set; } = null!;

    public string Flat { get; private set; } = null!;

    public decimal Subtotal { get; private set; }

    public decimal DeliveryCharge { get; private set; }

    public decimal Total { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Sent by the app with each checkout, so a repeated tap returns the same order.</summary>
    public Guid IdempotencyKey { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusChange> History => _history;

    public bool IsDueToExpire(DateTime now) => Status == OrderStatus.Placed && now >= PlacedAt + AcceptWithin;

    public void Accept(Guid ownerUserId, DateTime now) =>
        Move(OrderStatus.Accepted, ownerUserId, null, now, OrderStatus.Placed);

    public void Reject(Guid ownerUserId, string reason, DateTime now) =>
        Move(OrderStatus.Rejected, ownerUserId, RequireReason(reason), now, OrderStatus.Placed);

    /// <summary>The owner's next step: Accepted → Preparing → OutForDelivery → Delivered.</summary>
    public void Advance(Guid ownerUserId, DateTime now)
    {
        var next = Status switch
        {
            OrderStatus.Accepted => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.OutForDelivery,
            OrderStatus.OutForDelivery => OrderStatus.Delivered,
            _ => throw new InvalidOperationException($"A {Describe(Status)} order has no next step."),
        };

        Move(next, ownerUserId, null, now, Status);
    }

    public void CancelByCustomer(Guid customerUserId, DateTime now)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOperationException(
                "The business has already accepted this order. Please contact them to cancel it.");
        }

        Move(OrderStatus.CancelledByCustomer, customerUserId, null, now, OrderStatus.Placed);
    }

    public void CancelByOwner(Guid ownerUserId, string reason, DateTime now) =>
        Move(OrderStatus.CancelledByOwner, ownerUserId, RequireReason(reason), now,
            OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.OutForDelivery);

    /// <summary>Nobody accepted it in time. Recorded at the moment it became due, not when noticed.</summary>
    public void Expire(DateTime now)
    {
        if (!IsDueToExpire(now))
        {
            throw new InvalidOperationException("Only a Placed order past its acceptance time can expire.");
        }

        Move(OrderStatus.Expired, changedByUserId: null, reason: null, PlacedAt + AcceptWithin, OrderStatus.Placed);
    }

    private void Move(OrderStatus to, Guid? changedByUserId, string? reason, DateTime now, params OrderStatus[] from)
    {
        if (!from.Contains(Status))
        {
            throw new InvalidOperationException($"A {Describe(Status)} order cannot become {Describe(to)}.");
        }

        Record(to, changedByUserId, reason, now);
    }

    private void Record(OrderStatus status, Guid? changedByUserId, string? reason, DateTime now)
    {
        Status = status;
        StatusChangedAt = now;
        _history.Add(new OrderStatusChange(this, status, changedByUserId, reason, now));
    }

    private static string RequireReason(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("A reason is required.", nameof(reason))
            : reason.Trim();

    private static string Describe(OrderStatus status) => status switch
    {
        OrderStatus.OutForDelivery => "out for delivery",
        OrderStatus.CancelledByCustomer or OrderStatus.CancelledByOwner => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}

/// <summary>Who ordered and where to deliver, copied at order time.</summary>
public sealed record OrderCustomer(string Name, string Phone, string Tower, string Flat);
