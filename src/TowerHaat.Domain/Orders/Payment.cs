using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Orders;

public enum PaymentStatus
{
    Pending = 0,
    CustomerMarkedPaid = 1,
    Confirmed = 2,
    Collected = 3,
    Disputed = 4,
    Refunded = 5,
}

/// <summary>
/// Whether an order has been paid. Money never passes through TowerHaat: UPI goes straight to the
/// business's UPI ID and cash is handed over at the door, so this only records what the customer
/// and the owner say happened (C-08, B-08).
/// <code>
/// UPI: Pending → CustomerMarkedPaid → Confirmed; CustomerMarkedPaid → Disputed → CustomerMarkedPaid or Confirmed
/// COD: Pending → Collected
/// Any payment that changed hands → Refunded
/// </code>
/// </summary>
public class Payment : Entity, ITenantEntity
{
    private Payment()
    {
    }

    internal Payment(Order order)
    {
        SocietyId = order.SocietyId;
        OrderId = order.Id;
        Status = PaymentStatus.Pending;
    }

    public Guid SocietyId { get; set; }

    public Guid OrderId { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>UPI transaction reference the customer may give when marking it paid.</summary>
    public string? Utr { get; private set; }

    public DateTime? MarkedPaidAt { get; private set; }

    /// <summary>When the owner confirmed UPI received or cash collected.</summary>
    public DateTime? SettledAt { get; private set; }

    public DateTime? RefundedAt { get; private set; }

    /// <summary>The owner's note when disputing, shown to the customer.</summary>
    public string? Note { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Money moved, or the customer says it did.</summary>
    public bool IsPaidOrClaimed =>
        Status is PaymentStatus.CustomerMarkedPaid or PaymentStatus.Confirmed or PaymentStatus.Collected or PaymentStatus.Disputed;

    internal void MarkPaid(string? utr, DateTime now)
    {
        Require(PaymentStatus.Pending, PaymentStatus.Disputed);
        Status = PaymentStatus.CustomerMarkedPaid;
        Utr = utr ?? Utr;
        MarkedPaidAt = now;
        Note = null;
        UpdatedAt = now;
    }

    internal void Confirm(DateTime now)
    {
        Require(PaymentStatus.Pending, PaymentStatus.CustomerMarkedPaid, PaymentStatus.Disputed);
        Status = PaymentStatus.Confirmed;
        SettledAt = now;
        Note = null;
        UpdatedAt = now;
    }

    internal void Collect(DateTime now)
    {
        Require(PaymentStatus.Pending);
        Status = PaymentStatus.Collected;
        SettledAt = now;
        UpdatedAt = now;
    }

    internal void Dispute(string? note, DateTime now)
    {
        Require(PaymentStatus.CustomerMarkedPaid);
        Status = PaymentStatus.Disputed;
        Note = note;
        UpdatedAt = now;
    }

    internal void Refund(DateTime now)
    {
        if (!IsPaidOrClaimed)
        {
            throw new InvalidOperationException("Only a payment that was made can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        RefundedAt = now;
        UpdatedAt = now;
    }

    private void Require(params PaymentStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException(Status switch
            {
                PaymentStatus.Confirmed or PaymentStatus.Collected => "This payment is already settled.",
                PaymentStatus.Refunded => "This payment was refunded.",
                PaymentStatus.CustomerMarkedPaid => "The customer has already marked this paid.",
                _ => "The payment cannot change this way.",
            });
        }
    }
}
