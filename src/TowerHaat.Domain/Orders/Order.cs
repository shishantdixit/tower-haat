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

        Payment = new Payment(this);
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

    /// <summary>When the owner was reminded about this order still waiting to be accepted.</summary>
    public DateTime? ReminderSentAt { get; private set; }

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

    public Payment Payment { get; private set; } = null!;

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<OrderStatusChange> History => _history;

    /// <summary>Owners are reminded once, a third of the way to expiry, if they have not responded.</summary>
    public static readonly TimeSpan RemindAfter = TimeSpan.FromMinutes(10);

    public bool IsDueForReminder(DateTime now) =>
        Status == OrderStatus.Placed && ReminderSentAt is null && now >= PlacedAt + RemindAfter && !IsDueToExpire(now);

    public void MarkReminded(DateTime now) => ReminderSentAt = now;

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

    /// <summary>
    /// UPI can be paid once the business has accepted (rule 5), and until the order ends badly.
    /// A cancelled-by-owner order is not payable: any money already sent is refunded instead.
    /// </summary>
    public bool IsUpiPayable =>
        PaymentMethod == PaymentMethod.Upi
        && Status is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.OutForDelivery or OrderStatus.Delivered
        && Payment.Status is PaymentStatus.Pending or PaymentStatus.Disputed;

    /// <summary>C-08: the customer says they paid by UPI, optionally with the transaction reference.</summary>
    public void MarkPaidByCustomer(string? utr, DateTime now)
    {
        if (PaymentMethod != PaymentMethod.Upi)
        {
            throw new InvalidOperationException("This order is cash on delivery.");
        }

        if (!IsUpiPayable)
        {
            throw new InvalidOperationException(Status == OrderStatus.Placed
                ? "You can pay once the business accepts your order."
                : "This order cannot be paid now.");
        }

        Payment.MarkPaid(utr, now);
    }

    /// <summary>B-08: the owner saw the UPI money arrive.</summary>
    public void ConfirmUpiReceived(DateTime now)
    {
        RequireMethod(PaymentMethod.Upi);
        if (Status is OrderStatus.Placed or OrderStatus.Rejected or OrderStatus.Expired or OrderStatus.CancelledByCustomer)
        {
            throw new InvalidOperationException("Accept the order before confirming its payment.");
        }

        Payment.Confirm(now);
    }

    /// <summary>B-08: the owner took the cash at the door.</summary>
    public void MarkCashCollected(DateTime now)
    {
        RequireMethod(PaymentMethod.Cod);
        if (Status is not (OrderStatus.OutForDelivery or OrderStatus.Delivered))
        {
            throw new InvalidOperationException("Cash is collected at delivery.");
        }

        Payment.Collect(now);
    }

    /// <summary>The customer says they paid but no money arrived.</summary>
    public void DisputePayment(string? note, DateTime now)
    {
        RequireMethod(PaymentMethod.Upi);
        Payment.Dispute(note, now);
    }

    /// <summary>The owner returned the money, outside TowerHaat.</summary>
    public void MarkRefunded(DateTime now) => Payment.Refund(now);

    private void RequireMethod(PaymentMethod method)
    {
        if (PaymentMethod != method)
        {
            throw new InvalidOperationException(method == PaymentMethod.Upi ? "This order is cash on delivery." : "This order is paid by UPI.");
        }
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
